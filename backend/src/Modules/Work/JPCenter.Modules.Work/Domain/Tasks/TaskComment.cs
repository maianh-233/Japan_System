using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Tasks;

/// <summary>Bình luận trong task. Bảng: work.task_comments</summary>
public sealed class TaskComment
{
    private TaskComment() { }

    internal TaskComment(string text, Guid by, DateTimeOffset at) => (Id, Text, By, At) = (IdGenerator.NewId(), text, by, at);

    public Guid Id { get; private set; }
    public string Text { get; private set; } = null!;
    public Guid By { get; private set; }
    public DateTimeOffset At { get; private set; }
}
