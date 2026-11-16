using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>
/// Lộ trình = nội dung giảng dạy: Lesson → Chapter → Material; mốc thi chèn SAU 1 lesson (chỉ đánh dấu, không chứa đề).
/// Đã phát hành thì khóa nội dung — muốn sửa → NewVersion() tạo bản nháp. Bảng: academic.curricula
/// (+ lessons, chapters, materials, exam_milestones)
/// </summary>
public sealed class Curriculum : AggregateRoot<CurriculumId>, IAuditable, ISoftDeletable
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
    /// <summary>Phiên bản, vd "1.0", "1.1"</summary>
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

    public static Result<Curriculum> Create(string code, string name, JlptLevel level, CurriculumTier tier, string? entryRequirement,
        string? textbook, string? description, List<string> tags, Guid authorId)
    {
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("academic.curriculum.name_required", "Chưa nhập tên lộ trình");
        return new Curriculum
        {
            Id = CurriculumId.New(), Code = code, Name = name, Level = level, Tier = tier, EntryRequirement = entryRequirement,
            Textbook = textbook, Description = description, Tags = tags, AuthorId = authorId, Version = "1.0", Status = CurriculumStatus.Draft,
        };
    }

    public Result<Lesson> AddLesson(string title, bool hasQuiz)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        var lesson = new Lesson(title, _lessons.Count + 1, hasQuiz);
        _lessons.Add(lesson);
        return lesson;
    }

    public Result<Chapter> AddChapter(LessonId lessonId, string title)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        var lesson = _lessons.FirstOrDefault(l => l.Id == lessonId);
        if (lesson is null) return CurriculumErrors.LessonNotFound;
        return lesson.AddChapter(title);
    }

    public Result<Material> AddMaterial(LessonId lessonId, ChapterId chapterId, string name, MaterialType type, string url, long? size,
        bool sharedWithStudents)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        var chapter = _lessons.FirstOrDefault(l => l.Id == lessonId)?.Chapters.FirstOrDefault(c => c.Id == chapterId);
        if (chapter is null) return CurriculumErrors.ChapterNotFound;
        return chapter.AddMaterial(name, type, url, size, sharedWithStudents);
    }

    public Result<ExamMilestone> AddExam(ExamKind kind, string title, LessonId afterLessonId, string? note)
    {
        if (EnsureDraft() is { IsFailure: true } r) return r.Error;
        if (_lessons.All(l => l.Id != afterLessonId)) return CurriculumErrors.LessonNotFound;
        var exam = new ExamMilestone(kind, title, afterLessonId, note);
        _exams.Add(exam);
        return exam;
    }

    public Result Publish()
    {
        if (Status != CurriculumStatus.Draft) return CurriculumErrors.NotDraft;
        if (_lessons.Count == 0) return Error.Conflict("academic.curriculum.empty", "Lộ trình chưa có bài học");
        Status = CurriculumStatus.Published;
        return Result.Success();
    }

    public void Archive() => Status = CurriculumStatus.Archived;

    /// <summary>Tạo bản nháp phiên bản mới (sao chép nội dung) — lớp đang học vẫn giữ bản cũ</summary>
    public Curriculum NewVersion(string version, Guid authorId)
    {
        var copy = new Curriculum
        {
            Id = CurriculumId.New(), Code = Code, Name = Name, Level = Level, Tier = Tier, EntryRequirement = EntryRequirement,
            Textbook = Textbook, Description = Description, Tags = [.. Tags], AuthorId = authorId, Version = version,
            PreviousVersionId = Id, Status = CurriculumStatus.Draft,
        };
        var lessonMap = new Dictionary<LessonId, LessonId>();
        foreach (var l in _lessons.OrderBy(l => l.Order))
        {
            var nl = l.CloneInto();
            lessonMap[l.Id] = nl.Id;
            copy._lessons.Add(nl);
        }
        foreach (var e in _exams) copy._exams.Add(new ExamMilestone(e.Kind, e.Title, lessonMap[e.AfterLessonId], e.Note));
        return copy;
    }

    private Result EnsureDraft() => Status == CurriculumStatus.Draft ? Result.Success() : CurriculumErrors.NotDraft;
}
