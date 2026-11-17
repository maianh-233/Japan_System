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

    internal void Update(string title, bool hasQuiz) => (Title, HasQuiz) = (title, hasQuiz);

    internal void SetOrder(int order) => Order = order;

    internal Chapter AddChapter(string title)
    {
        var c = new Chapter(title, _chapters.Count == 0 ? 1 : _chapters.Max(x => x.Order) + 1);
        _chapters.Add(c);
        return c;
    }

    internal Chapter? RemoveChapter(ChapterId id)
    {
        var c = _chapters.FirstOrDefault(x => x.Id == id);
        if (c is null) return null;
        _chapters.Remove(c);
        Renumber();
        return c;
    }

    /// <summary>Đổi thứ tự chapter — <paramref name="ids"/> phải đúng đủ các chapter hiện có</summary>
    internal bool Reorder(IReadOnlyList<ChapterId> ids)
    {
        if (ids.Count != _chapters.Count || ids.Distinct().Count() != ids.Count || ids.Any(id => _chapters.All(c => c.Id != id))) return false;
        for (var i = 0; i < ids.Count; i++) _chapters.First(c => c.Id == ids[i]).SetOrder(i + 1);
        return true;
    }

    internal Lesson CloneInto()
    {
        var l = new Lesson(Title, Order, HasQuiz);
        foreach (var c in _chapters.OrderBy(c => c.Order)) l._chapters.Add(c.CloneInto());
        return l;
    }

    private void Renumber()
    {
        var i = 1;
        foreach (var c in _chapters.OrderBy(c => c.Order)) c.SetOrder(i++);
    }
}
