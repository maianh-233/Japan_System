using System.Text;
using JPCenter.Application.Common.Files;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp khóa học + lộ trình mẫu từ <c>academic.seed.json</c> (xuất từ mock frontend). Chỉ chạy khi bảng khóa học còn trống — không xóa gì.
/// Tài liệu dạng file trỏ tới 1 file mẫu cho mỗi đuôi (tải lên kho 1 lần, mọi tài liệu cùng đuôi dùng chung — giống bản sao dùng chung file).
/// Kho chưa cấu hình / không chạy → chỉ lưu key, link tải sẽ không mở được.
/// </summary>
internal sealed class AcademicSeeder(AcademicDbContext db, IFileStorage storage, ILogger<AcademicSeeder> logger) : IDataSeeder
{
    public int Order => 30;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Courses.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<AcademicSeedFile>(typeof(AcademicSeeder).Assembly, "academic.seed.json");
        var demoFiles = new Dictionary<string, StoredFile>();

        foreach (var s in data.Curricula)
        {
            var c = Curriculum.Create(s.Code, s.Name, Level(s.Level), SnakeCaseEnumConverter<CurriculumTier>.Parse(s.Tier), s.EntryRequirement,
                s.Version, s.Textbook, s.Description, s.Tags, s.AuthorId is null ? Guid.Empty : SeedData.IdOf(s.AuthorId)).Value;

            var lessonIds = new Dictionary<string, LessonId>();
            foreach (var sl in s.Lessons)
            {
                var lesson = c.AddLesson(sl.Title, sl.HasQuiz ?? false).Value;
                lessonIds[sl.Id] = lesson.Id;
                foreach (var sc in sl.Chapters)
                {
                    var chapter = c.AddChapter(lesson.Id, sc.Title).Value;
                    foreach (var m in sc.Materials)
                    {
                        var type = SnakeCaseEnumConverter<MaterialType>.Parse(m.Type);
                        if (MaterialTypes.IsLink(type))
                        {
                            c.AddLinkMaterial(lesson.Id, chapter.Id, m.Name, type, m.Url, m.SharedWithStudents);
                            continue;
                        }
                        var fileName = Path.HasExtension(m.Name) ? m.Name : $"{m.Name}.{DefaultExtension(type)}";
                        var file = await DemoFileAsync(Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(), demoFiles, ct);
                        c.AddFileMaterial(lesson.Id, chapter.Id, m.Name, file.Key, fileName, file.ContentType, m.Size ?? file.Size,
                            m.SharedWithStudents);
                    }
                }
            }
            foreach (var e in s.Exams)
                c.AddExam(SnakeCaseEnumConverter<ExamKind>.Parse(e.Kind), e.Title, lessonIds[e.AfterLessonId], e.Note);

            if (s.Status is "published" or "archived") c.Publish();
            if (s.Status == "archived") c.Archive();
            db.AddWithId(c, SeedData.IdOf(s.Id));
        }

        var curriculumStatus = data.Curricula.ToDictionary(c => c.Id, c => c.Status);
        foreach (var s in data.Courses)
        {
            var course = Course.Create(s.Code, s.Name, SnakeCaseEnumConverter<CourseCategory>.Parse(s.Category), Level(s.LevelFrom),
                Level(s.LevelTo), SnakeCaseEnumConverter<CourseFormat>.Parse(s.Format), SnakeCaseEnumConverter<TeacherType>.Parse(s.TeacherType),
                SnakeCaseEnumConverter<StudyMode>.Parse(s.Mode), s.Sessions, s.MinutesPerSession, s.SessionsPerWeek, Money.FromTrusted(s.Fee),
                s.MaxStudents, s.BranchIds.Select(b => new BranchId(SeedData.IdOf(b))).ToList(), s.Description).Value;

            var links = data.CourseCurricula.Where(l => l.CourseId == s.Id).OrderByDescending(l => l.IsDefault).ToList();
            foreach (var l in links)
                course.LinkCurriculum(new CurriculumId(SeedData.IdOf(l.CurriculumId)), l.IsDefault, DateTimeOffset.Parse(l.LinkedAt,
                    System.Globalization.CultureInfo.InvariantCulture));

            var defaultPublished = links.FirstOrDefault(l => l.IsDefault) is { } d && curriculumStatus[d.CurriculumId] == "published";
            if (s.Status is "active" or "paused" && course.Activate(defaultPublished).IsFailure)
                logger.LogWarning("Khóa mẫu {Code} chưa có lộ trình mặc định đã phát hành — giữ trạng thái nháp", s.Code);
            if (s.Status == "paused") course.Pause();
            db.AddWithId(course, SeedData.IdOf(s.Id));
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<StoredFile> DemoFileAsync(string extension, Dictionary<string, StoredFile> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(extension, out var cached)) return cached;
        var name = $"tai-lieu-mau.{extension}";
        var bytes = DemoContent(extension);
        StoredFile Fallback() => new($"academic/materials/demo/{name}", name, "application/octet-stream", bytes.Length);

        StoredFile file;
        try
        {
            var r = await storage.UploadAsync(new MemoryStream(bytes), name, "academic/materials/demo", FilePolicy.TeachingMaterials, ct);
            file = r.IsSuccess ? r.Value : Fallback();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning("Không tải được file mẫu .{Extension} lên kho ({Message}) — tài liệu mẫu chỉ có key", extension, e.Message);
            file = Fallback();
        }
        cache[extension] = file;
        return file;
    }

    /// <summary>PDF mẫu mở được thật; đuôi khác chỉ có phần đầu đúng định dạng (qua được kiểm tra nội dung, không mở được bằng Office)</summary>
    private static byte[] DemoContent(string extension) => extension switch
    {
        "pdf" => MinimalPdf("JP Center - Tai lieu mau"),
        "docx" or "xlsx" or "pptx" => [0x50, 0x4B, 0x03, 0x04, .. new byte[60]],
        "doc" or "xls" or "ppt" => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, .. new byte[56]],
        "mp3" => [.. "ID3"u8, 0x04, .. new byte[60]],
        "wav" => [.. "RIFF"u8, 0, 0, 0, 0, .. "WAVE"u8, .. new byte[52]],
        "m4a" => [0, 0, 0, 0x20, .. "ftypM4A "u8, .. new byte[52]],
        _ => Encoding.UTF8.GetBytes("Tai lieu mau"),
    };

    private static string DefaultExtension(MaterialType type) => type switch
    {
        MaterialType.Doc => "docx",
        MaterialType.Ppt => "pptx",
        MaterialType.Xls => "xlsx",
        MaterialType.Audio => "mp3",
        _ => "pdf",
    };

    /// <summary>PDF 1 trang 1 dòng chữ — tự tính bảng xref</summary>
    private static byte[] MinimalPdf(string text)
    {
        var content = $"BT /F1 24 Tf 72 720 Td ({text}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = sb.Length;
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static JlptLevel Level(string level) => SnakeCaseEnumConverter<JlptLevel>.Parse(level);

    private sealed record AcademicSeedFile(List<SeedCourse> Courses, List<SeedCurriculum> Curricula, List<SeedLink> CourseCurricula);

    private sealed record SeedCourse(string Id, string Code, string Name, string Category, string LevelFrom, string LevelTo, string Format,
        string TeacherType, string Mode, int Sessions, int MinutesPerSession, int SessionsPerWeek, decimal Fee, int? MaxStudents,
        List<string> BranchIds, string Status, string? Description);

    private sealed record SeedCurriculum(string Id, string Code, string Name, string Level, string Tier, string? EntryRequirement,
        string Version, string? Textbook, string? Description, List<string> Tags, string Status, string? AuthorId, List<SeedLesson> Lessons,
        List<SeedExam> Exams);

    private sealed record SeedLesson(string Id, string Title, bool? HasQuiz, List<SeedChapter> Chapters);

    private sealed record SeedChapter(string Id, string Title, List<SeedMaterial> Materials);

    private sealed record SeedMaterial(string Id, string Name, string Type, string Url, long? Size, bool SharedWithStudents);

    private sealed record SeedExam(string Id, string Kind, string Title, string AfterLessonId, string? Note);

    private sealed record SeedLink(string CourseId, string CurriculumId, bool IsDefault, string LinkedAt);
}
