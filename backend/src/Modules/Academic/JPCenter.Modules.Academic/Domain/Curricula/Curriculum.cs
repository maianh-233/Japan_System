using System.Text.RegularExpressions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>
/// Lộ trình = nội dung giảng dạy: Lesson → Chapter → Material; mốc thi chèn SAU 1 lesson (chỉ đánh dấu, không chứa đề).
/// Đã phát hành thì khóa nội dung — muốn sửa → <see cref="NewVersion"/> tạo bản nháp. Bảng: academic.curricula
/// (+ lessons, chapters, materials, exam_milestones). User story 09 — LT-01 → LT-11.
/// </summary>
public sealed partial class Curriculum : AggregateRoot<CurriculumId>, IAuditable, ISoftDeletable
{
    private readonly List<Lesson> _lessons = [];
    private readonly List<ExamMilestone> _exams = [];

    private Curriculum() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public JlptLevel Level { get; private set; }
    public CurriculumTier Tier { get; private set; }
    /// <summary>Điều kiện áp dụng (bậc nâng cao), vd "Điểm test đầu vào TB lớp ≥ 80%"</summary>
    public string? EntryRequirement { get; private set; }
    /// <summary>Phiên bản dạng "v1.0", "v1.1" (giống frontend)</summary>
    public string Version { get; private set; } = null!;
    /// <summary>Phiên bản trước (tạo bằng NewVersion)</summary>
    public CurriculumId? PreviousVersionId { get; private set; }
    /// <summary>Giáo trình, vd Minna no Nihongo</summary>
    public string? Textbook { get; private set; }
    public string? Description { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public CurriculumStatus Status { get; private set; }
    /// <summary>Người soạn (Hr.Employee)</summary>
    public Guid AuthorId { get; private set; }
    public IReadOnlyList<Lesson> Lessons => _lessons;
    public IReadOnlyList<ExamMilestone> Exams => _exams;

    public bool IsDraft => Status == CurriculumStatus.Draft;

    /// <summary>Mọi tài liệu trong lộ trình</summary>
    public IEnumerable<Material> AllMaterials => _lessons.SelectMany(l => l.Chapters).SelectMany(c => c.Materials);

    public static Result<Curriculum> Create(string code, string name, JlptLevel level, CurriculumTier tier, string? entryRequirement,
        string version, string? textbook, string? description, List<string> tags, Guid authorId)
    {
        var c = new Curriculum { Id = CurriculumId.New(), AuthorId = authorId, Status = CurriculumStatus.Draft };
        var set = c.ApplyInfo(code, name, level, tier, entryRequirement, version, textbook, description, tags);
        return set.IsFailure ? set.Error : c;
    }

    /// <summary>
    /// Sửa thông tin (LT-04). Bản nháp sửa mọi thứ; bản đã phát hành / lưu trữ chỉ sửa mô tả + thẻ (E1) — đổi trường khác → not_draft.
    /// </summary>
    public Result UpdateInfo(string code, string name, JlptLevel level, CurriculumTier tier, string? entryRequirement, string version,
        string? textbook, string? description, List<string> tags)
    {
        if (IsDraft) return ApplyInfo(code, name, level, tier, entryRequirement, version, textbook, description, tags);

        var locked = NormalizeCode(code) != Code || name.Trim() != Name || level != Level || tier != Tier
            || version.Trim() != Version || Clean(entryRequirement) != EntryRequirement || Clean(textbook) != Textbook;
        if (locked) return CurriculumErrors.NotDraft;
        Description = Clean(description);
        Tags = CleanTags(tags);
        return Result.Success();
    }

    // ---------------- Bài học (LT-05) ----------------

    public Result<Lesson> AddLesson(string title, bool hasQuiz)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        if (string.IsNullOrWhiteSpace(title)) return CurriculumErrors.TitleRequired;
        var lesson = new Lesson(title.Trim(), _lessons.Count == 0 ? 1 : _lessons.Max(l => l.Order) + 1, hasQuiz);
        _lessons.Add(lesson);
        return lesson;
    }

    public Result UpdateLesson(LessonId lessonId, string title, bool hasQuiz)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r;
        if (string.IsNullOrWhiteSpace(title)) return CurriculumErrors.TitleRequired;
        var lesson = FindLesson(lessonId);
        if (lesson is null) return CurriculumErrors.LessonNotFound;
        lesson.Update(title.Trim(), hasQuiz);
        return Result.Success();
    }

    /// <summary>
    /// Xóa bài (kèm chapter + tài liệu). Mốc thi sau bài này chuyển sang sau bài liền trước (không có → liền sau; không còn bài → xóa mốc).
    /// Trả về tài liệu bị xóa để handler biết file nào có thể dọn.
    /// </summary>
    public Result<IReadOnlyList<Material>> RemoveLesson(LessonId lessonId)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        var lesson = FindLesson(lessonId);
        if (lesson is null) return CurriculumErrors.LessonNotFound;

        var ordered = _lessons.OrderBy(l => l.Order).ToList();
        var index = ordered.IndexOf(lesson);
        var fallback = index > 0 ? ordered[index - 1] : ordered.ElementAtOrDefault(index + 1);
        foreach (var exam in _exams.Where(e => e.AfterLessonId == lessonId).ToList())
        {
            if (fallback is null) _exams.Remove(exam);
            else exam.MoveAfter(fallback.Id);
        }

        var removed = lesson.Chapters.SelectMany(c => c.Materials).ToList();
        _lessons.Remove(lesson);
        RenumberLessons();
        return removed;
    }

    /// <summary>Đổi thứ tự bài — gửi đủ Id các bài theo thứ tự mới (LT-05.E4)</summary>
    public Result ReorderLessons(IReadOnlyList<LessonId> ids)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r;
        if (ids.Count != _lessons.Count || ids.Distinct().Count() != ids.Count || ids.Any(id => FindLesson(id) is null))
            return CurriculumErrors.InvalidOrder;
        for (var i = 0; i < ids.Count; i++) FindLesson(ids[i])!.SetOrder(i + 1);
        return Result.Success();
    }

    // ---------------- Chapter ----------------

    public Result<Chapter> AddChapter(LessonId lessonId, string title)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        if (string.IsNullOrWhiteSpace(title)) return CurriculumErrors.TitleRequired;
        var lesson = FindLesson(lessonId);
        if (lesson is null) return CurriculumErrors.LessonNotFound;
        return lesson.AddChapter(title.Trim());
    }

    public Result RenameChapter(LessonId lessonId, ChapterId chapterId, string title)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r;
        if (string.IsNullOrWhiteSpace(title)) return CurriculumErrors.TitleRequired;
        var chapter = FindChapter(lessonId, chapterId);
        if (chapter is null) return CurriculumErrors.ChapterNotFound;
        chapter.Rename(title.Trim());
        return Result.Success();
    }

    public Result<IReadOnlyList<Material>> RemoveChapter(LessonId lessonId, ChapterId chapterId)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        var lesson = FindLesson(lessonId);
        if (lesson is null) return CurriculumErrors.LessonNotFound;
        var chapter = lesson.RemoveChapter(chapterId);
        if (chapter is null) return CurriculumErrors.ChapterNotFound;
        return chapter.Materials.ToList();
    }

    public Result ReorderChapters(LessonId lessonId, IReadOnlyList<ChapterId> ids)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r;
        var lesson = FindLesson(lessonId);
        if (lesson is null) return CurriculumErrors.LessonNotFound;
        return lesson.Reorder(ids) ? Result.Success() : CurriculumErrors.InvalidOrder;
    }

    // ---------------- Tài liệu (LT-06) ----------------

    /// <summary>Thêm tài liệu file đã lưu vào kho. Loại tài liệu suy từ đuôi tệp</summary>
    public Result<Material> AddFileMaterial(LessonId lessonId, ChapterId chapterId, string? name, string fileKey, string fileName,
        string contentType, long size, bool sharedWithStudents)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        var chapter = FindChapter(lessonId, chapterId);
        if (chapter is null) return CurriculumErrors.ChapterNotFound;
        var display = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(fileName) : name.Trim();
        var material = Material.File(display, MaterialTypes.FromFileName(fileName), fileKey, fileName, contentType, size, sharedWithStudents);
        chapter.Add(material);
        return material;
    }

    /// <summary>Thêm tài liệu link (video YouTube / Drive / web)</summary>
    public Result<Material> AddLinkMaterial(LessonId lessonId, ChapterId chapterId, string name, MaterialType type, string url,
        bool sharedWithStudents)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        if (string.IsNullOrWhiteSpace(name)) return CurriculumErrors.TitleRequired;
        if (!MaterialTypes.IsLink(type)) return CurriculumErrors.LinkTypeRequired;
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return CurriculumErrors.InvalidLink;
        if (type == MaterialType.Drive && uri.Host is not ("drive.google.com" or "docs.google.com")) return CurriculumErrors.NotDriveLink;
        var chapter = FindChapter(lessonId, chapterId);
        if (chapter is null) return CurriculumErrors.ChapterNotFound;
        var material = Material.Link(name.Trim(), type, uri.ToString(), sharedWithStudents);
        chapter.Add(material);
        return material;
    }

    public Result UpdateMaterial(MaterialId materialId, string name, bool sharedWithStudents)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r;
        if (string.IsNullOrWhiteSpace(name)) return CurriculumErrors.TitleRequired;
        var material = FindMaterial(materialId);
        if (material is null) return CurriculumErrors.MaterialNotFound;
        material.Update(name.Trim(), sharedWithStudents);
        return Result.Success();
    }

    public Result<Material> RemoveMaterial(MaterialId materialId)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        foreach (var chapter in _lessons.SelectMany(l => l.Chapters))
            if (chapter.Remove(materialId) is { } removed) return removed;
        return CurriculumErrors.MaterialNotFound;
    }

    public Material? FindMaterial(MaterialId materialId) => AllMaterials.FirstOrDefault(m => m.Id == materialId);

    // ---------------- Mốc thi (LT-08) ----------------

    public Result<ExamMilestone> AddExam(ExamKind kind, string title, LessonId afterLessonId, string? note)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        if (string.IsNullOrWhiteSpace(title)) return CurriculumErrors.TitleRequired;
        if (FindLesson(afterLessonId) is null) return CurriculumErrors.LessonNotFound;
        var exam = new ExamMilestone(kind, title.Trim(), afterLessonId, Clean(note));
        _exams.Add(exam);
        return exam;
    }

    public Result UpdateExam(ExamMilestoneId examId, ExamKind kind, string title, LessonId afterLessonId, string? note)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r;
        if (string.IsNullOrWhiteSpace(title)) return CurriculumErrors.TitleRequired;
        var exam = _exams.FirstOrDefault(e => e.Id == examId);
        if (exam is null) return CurriculumErrors.ExamNotFound;
        if (FindLesson(afterLessonId) is null) return CurriculumErrors.LessonNotFound;
        exam.Update(kind, title.Trim(), afterLessonId, Clean(note));
        return Result.Success();
    }

    public Result RemoveExam(ExamMilestoneId examId)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r;
        var exam = _exams.FirstOrDefault(e => e.Id == examId);
        if (exam is null) return CurriculumErrors.ExamNotFound;
        _exams.Remove(exam);
        return Result.Success();
    }

    // ---------------- Vòng đời (LT-09 → LT-11) ----------------

    public Result Publish()
    {
        if (!IsDraft) return CurriculumErrors.NotDraft;
        if (_lessons.Count == 0) return CurriculumErrors.Empty;
        Status = CurriculumStatus.Published;
        return Result.Success();
    }

    /// <summary>Lưu trữ — chỉ bản đã phát hành. Đang là mặc định của khóa → handler chặn trước (LT-11.E1)</summary>
    public Result Archive()
    {
        if (Status != CurriculumStatus.Published) return CurriculumErrors.NotPublished;
        Status = CurriculumStatus.Archived;
        return Result.Success();
    }

    public Result Restore()
    {
        if (Status != CurriculumStatus.Archived) return CurriculumErrors.NotArchived;
        Status = CurriculumStatus.Published;
        return Result.Success();
    }

    /// <summary>Tạo bản nháp phiên bản kế tiếp (sao chép nội dung, dùng chung file) — lớp đang học vẫn giữ bản cũ (LT-10)</summary>
    public Result<Curriculum> NewVersion(Guid authorId)
    {
        if (Status != CurriculumStatus.Published) return CurriculumErrors.NotPublished;
        var copy = CopyContent(new Curriculum
        {
            Id = CurriculumId.New(), Code = Code, Name = Name, Level = Level, Tier = Tier, EntryRequirement = EntryRequirement,
            Textbook = Textbook, Description = Description, Tags = [.. Tags], AuthorId = authorId, Version = NextVersion(Version),
            PreviousVersionId = Id, Status = CurriculumStatus.Draft,
        });
        return copy;
    }

    /// <summary>Nhân bản thành lộ trình khác (LT-03 — vd bản nâng cao từ bản chuẩn): thông tin mới, nội dung sao chép, không nối phiên bản</summary>
    public Result<Curriculum> CloneAs(string code, string name, JlptLevel level, CurriculumTier tier, string? entryRequirement, string version,
        string? textbook, string? description, List<string> tags, Guid authorId)
    {
        var created = Create(code, name, level, tier, entryRequirement, version, textbook, description, tags, authorId);
        return created.IsFailure ? created.Error : CopyContent(created.Value);
    }

    /// <summary>"v1.3" → "v1.4" (giữ dạng có / không chữ v)</summary>
    public static string NextVersion(string version)
    {
        var m = VersionNumbers().Match(version ?? "");
        if (!m.Success) return "v1.0";
        var minor = int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) + 1;
        return $"v{m.Groups[1].Value}.{minor}";
    }

    public static string NormalizeCode(string? code) => (code ?? "").Trim().ToUpperInvariant();

    private Curriculum CopyContent(Curriculum target)
    {
        var lessonMap = new Dictionary<LessonId, LessonId>();
        foreach (var l in _lessons.OrderBy(l => l.Order))
        {
            var nl = l.CloneInto();
            lessonMap[l.Id] = nl.Id;
            target._lessons.Add(nl);
        }
        foreach (var e in _exams.Where(e => lessonMap.ContainsKey(e.AfterLessonId)))
            target._exams.Add(new ExamMilestone(e.Kind, e.Title, lessonMap[e.AfterLessonId], e.Note));
        return target;
    }

    private Result ApplyInfo(string code, string name, JlptLevel level, CurriculumTier tier, string? entryRequirement, string version,
        string? textbook, string? description, List<string> tags)
    {
        code = NormalizeCode(code);
        version = (version ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return CurriculumErrors.NameRequired;
        if (!CodePattern().IsMatch(code)) return CurriculumErrors.InvalidCode;
        if (!VersionPattern().IsMatch(version)) return CurriculumErrors.InvalidVersion;
        if (tier == CurriculumTier.Advanced && string.IsNullOrWhiteSpace(entryRequirement)) return CurriculumErrors.EntryRequirementRequired;

        Code = code;
        Name = name.Trim();
        Level = level;
        Tier = tier;
        EntryRequirement = tier == CurriculumTier.Advanced ? Clean(entryRequirement) : null;
        Version = version;
        Textbook = Clean(textbook);
        Description = Clean(description);
        Tags = CleanTags(tags);
        return Result.Success();
    }

    private Lesson? FindLesson(LessonId id) => _lessons.FirstOrDefault(l => l.Id == id);

    private Chapter? FindChapter(LessonId lessonId, ChapterId chapterId) => FindLesson(lessonId)?.Chapters.FirstOrDefault(c => c.Id == chapterId);

    private void RenumberLessons()
    {
        var i = 1;
        foreach (var l in _lessons.OrderBy(l => l.Order)) l.SetOrder(i++);
    }

    private Result EnsureDraft() => IsDraft ? Result.Success() : CurriculumErrors.NotDraft;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static List<string> CleanTags(IEnumerable<string>? tags) =>
        (tags ?? []).Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    [GeneratedRegex("^[A-Z0-9-]{2,30}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^v\d+\.\d+$")]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"^v?(\d+)\.(\d+)$")]
    private static partial Regex VersionNumbers();
}
