using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Bài học trong lộ trình (có thứ tự). Bảng: academic.lessons</summary>
public sealed class Lesson : Entity<LessonId>
{
    private readonly List<Chapter> _chapters = [];

    private Lesson() { }

    internal Lesson(string title, int order, bool hasQuiz) : base(LessonId.New()) => (Title, Order, HasQuiz) = (title, order, hasQuiz);

    public string Title { get; private set; } = null!;
    /// <summary>Thứ tự trong lộ trình (1-based)</summary>
    public int Order { get; private set; }
    /// <summary>Có bài kiểm tra nhỏ cuối lesson</summary>
    public bool HasQuiz { get; private set; }
    public IReadOnlyList<Chapter> Chapters => _chapters;

    internal Chapter AddChapter(string title)
    {
        var c = new Chapter(title, _chapters.Count + 1);
        _chapters.Add(c);
        return c;
    }

    internal Lesson CloneInto()
    {
        var l = new Lesson(Title, Order, HasQuiz);
        foreach (var c in _chapters.OrderBy(c => c.Order)) l._chapters.Add(c.CloneInto());
        return l;
    }
}
