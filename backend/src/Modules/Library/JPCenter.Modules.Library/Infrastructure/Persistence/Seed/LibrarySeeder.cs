using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.Modules.Library.Domain.TeacherDocuments;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Library.Infrastructure.Persistence.Seed;

/// <summary>
/// Dữ liệu mẫu Library từ mock frontend (<c>npm run export-seed</c> → library.seed.json), mỗi nhóm chỉ nạp khi bảng trống:
/// tài liệu GV (giai đoạn 20) · thư viện điện tử (giai đoạn 23). File mẫu chỉ có khóa trong kho (<c>library/…/demo/…</c>) — chưa tải nội dung lên.
/// Mock chỉ có số liệu tổng (daily 14 ngày, top người đọc) → dựng lại lượt xem: đủ lượt 14 ngày theo <c>daily</c> + tối đa 200 lượt cũ hơn,
/// người xem xoay vòng trong top; số liệu tổng tính lại từ các lượt này (<see cref="LibraryStats"/>).
/// </summary>
internal sealed class LibrarySeeder(LibraryDbContext db, IClock clock, ILogger<LibrarySeeder> logger) : IDataSeeder
{
    public int Order => 60;

    private const int MaxOlderViews = 200;
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    public async Task SeedAsync(CancellationToken ct)
    {
        var data = SeedData.Read<LibrarySeedFile>(typeof(LibrarySeeder).Assembly, "library.seed.json");
        if (!await db.TeacherDocuments.IgnoreQueryFilters().AnyAsync(ct)) SeedTeacherDocs(data.TeacherDocs);
        if (!await db.Documents.IgnoreQueryFilters().AnyAsync(ct)) SeedDocuments(data.Documents ?? []);
        await db.SaveChangesAsync(ct);
    }

    private void SeedTeacherDocs(List<TeacherDocSeed> docs)
    {
        foreach (var d in docs)
        {
            var kind = SnakeCaseEnumConverter<TeacherDocKind>.Parse(d.Kind);
            var url = d.Url ?? $"library/teacher-docs/demo/{d.FileName}";
            var created = TeacherDocument.Create(SeedData.IdOf(d.TeacherId), d.Title, d.Description, new TeacherDocFile(d.FileName, kind, url, d.Size),
                d.ClassIds.Select(SeedData.IdOf).ToList(), d.SessionId is null ? null : SeedData.IdOf(d.SessionId));
            if (created.IsFailure)
            {
                logger.LogWarning("Bỏ qua tài liệu GV mẫu {Id}: {Error}", d.Id, created.Error.Message);
                continue;
            }
            var doc = created.Value;
            foreach (var studentId in d.ViewedBy) doc.MarkViewed(SeedData.IdOf(studentId), d.CreatedAt.AddHours(2));
            db.AddWithId(doc, SeedData.IdOf(d.Id));
            db.SetCreatedAt(doc, d.CreatedAt);
        }
        logger.LogInformation("Đã nạp {Count} tài liệu GV mẫu", docs.Count);
    }

    private void SeedDocuments(List<DocumentSeed> docs)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.ToOffset(Vietnam).DateTime);
        int count = 0, viewCount = 0;
        foreach (var d in docs)
        {
            var fileType = SnakeCaseEnumConverter<DocFileType>.Parse(d.FileType);
            var fileName = d.FileName ?? fileType switch
            {
                DocFileType.Audio => $"{d.Id}.mp3",
                DocFileType.Video => $"{d.Id}.mp4",
                DocFileType.Link => null,
                _ => $"{d.Id}.pdf",
            };
            var key = fileType == DocFileType.Link ? "https://www3.nhk.or.jp/news/easy/" : $"library/documents/demo/{fileName}";
            var info = new LibraryDocumentInfo(d.Title, d.Subtitle, d.Description, SnakeCaseEnumConverter<DocCategory>.Parse(d.Category), fileType, key,
                fileName, d.Size, d.Levels.Select(SnakeCaseEnumConverter<JlptLevel>.Parse).ToList(),
                d.Skills.Select(SnakeCaseEnumConverter<JapaneseSkill>.Parse).ToList(), d.Tags, d.Pages, d.Duration, d.Publisher, d.Year);
            var created = LibraryDocument.Create(info, d.Audience.Select(SnakeCaseEnumConverter<DocAudience>.Parse).ToList(),
                d.BranchIds.Select(b => new BranchId(SeedData.IdOf(b))).ToList(), d.UploadedById is null ? Guid.Empty : SeedData.IdOf(d.UploadedById),
                d.UploadedAt);
            if (created.IsFailure)
            {
                logger.LogWarning("Bỏ qua tài liệu thư viện mẫu {Id}: {Error}", d.Id, created.Error.Message);
                continue;
            }
            var doc = created.Value;
            doc.SetProtection(d.Watermark, d.AllowDownload);
            if (d.Status == "hidden") doc.Hide();
            db.AddWithId(doc, SeedData.IdOf(d.Id));
            db.SetCreatedAt(doc, d.UploadedAt);

            var views = BuildViews(doc, d, today);
            db.DocumentViews.AddRange(views);
            doc.ApplyStats(LibraryStats.From(views, today, Vietnam));
            count++;
            viewCount += views.Count;
        }
        logger.LogInformation("Đã nạp {Count} tài liệu thư viện mẫu, {Views} lượt xem", count, viewCount);
    }

    /// <summary>Lượt xem giả lập: theo daily 14 ngày + lượt cũ hơn (từ ngày tải lên), người xem xoay vòng trong top, phút đọc quanh mức TB của mock</summary>
    private static List<DocumentView> BuildViews(LibraryDocument doc, DocumentSeed d, DateOnly today)
    {
        var views = new List<DocumentView>();
        if (d.Stats.Top.Count == 0 || d.Stats.Views == 0) return views;
        var n = 0;
        void Add(DateOnly day)
        {
            var reader = d.Stats.Top[n % d.Stats.Top.Count];
            var teacher = reader.Role.StartsWith("Giáo viên", StringComparison.Ordinal);
            var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), Vietnam).AddMinutes(n * 37 % 720).ToUniversalTime();
            var v = DocumentView.Start(doc.Id, teacher ? ViewerType.Staff : ViewerType.Student, SeedData.IdOf("reader:" + reader.Name), reader.Name,
                reader.Role, null, start);
            v.End(start.AddMinutes((double)Math.Max(1, d.Stats.AvgMinutes + (n % 5) - 2)));
            views.Add(v);
            n++;
        }

        for (var i = 0; i < d.Stats.Daily.Count && i < LibraryStats.Days; i++)
            for (var k = 0; k < d.Stats.Daily[i]; k++) Add(today.AddDays(i - (d.Stats.Daily.Count - 1)));

        var uploaded = DateOnly.FromDateTime(d.UploadedAt.ToOffset(Vietnam).DateTime);
        var span = today.DayNumber - LibraryStats.Days - uploaded.DayNumber;
        var older = Math.Min(MaxOlderViews, Math.Max(0, d.Stats.Views - views.Count));
        for (var k = 0; k < older && span > 0; k++) Add(uploaded.AddDays(k * 13 % span));
        return views;
    }

    private sealed record LibrarySeedFile(List<TeacherDocSeed> TeacherDocs, List<DocumentSeed>? Documents);

    private sealed record TeacherDocSeed(string Id, string TeacherId, string Title, string? Description, string FileName, string Kind, long? Size,
        string? Url, List<string> ClassIds, string? SessionId, DateTimeOffset CreatedAt, List<string> ViewedBy);

    private sealed record ReaderSeed(string Name, string Role, int Views);

    private sealed record StatsSeed(int Views, decimal AvgMinutes, List<int> Daily, List<ReaderSeed> Top);

    private sealed record DocumentSeed(string Id, string Title, string? Subtitle, string? Description, string Category, string FileType,
        string? FileName, long? Size, int? Pages, int? Duration, List<string> Levels, List<string> Skills, List<string> Tags, string? Publisher,
        int? Year, List<string> Audience, List<string> BranchIds, bool Watermark, bool AllowDownload, string Status, string? UploadedById,
        DateTimeOffset UploadedAt, StatsSeed Stats);
}
