using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Tasks;

/// <summary>1 mục checklist của task. Bảng: work.task_checklist_items</summary>
public sealed class TaskChecklistItem
{
    private TaskChecklistItem() { }

    internal TaskChecklistItem(string text, int order) => (Id, Text, Order) = (IdGenerator.NewId(), text, order);

    public Guid Id { get; private set; }
    public string Text { get; private set; } = null!;
    public bool Done { get; private set; }
    public int Order { get; private set; }

    internal void Set(bool done) => Done = done;
}
