using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Tasks;

/// <summary>Bình luận trong task. Bảng: work.task_comments</summary>
public sealed class TaskComment
{
    private TaskComment() { }

    internal TaskComment(string text, Guid by, string byName, DateTimeOffset at) => (Id, Text, By, ByName, At) = (IdGenerator.NewId(), text, by, byName, at);

    public Guid Id { get; private set; }
    public string Text { get; private set; } = null!;
    /// <summary>Người viết (Hr.Employee)</summary>
    public Guid By { get; private set; }
    /// <summary>Tên người viết lúc viết</summary>
    public string ByName { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
}
