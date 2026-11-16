using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Handovers;

/// <summary>Việc tồn bàn giao cho ca sau. Bảng: work.handover_items</summary>
public sealed class HandoverItem
{
    private HandoverItem() { }

    internal HandoverItem(string text) => (Id, Text) = (IdGenerator.NewId(), text);

    public Guid Id { get; private set; }
    public string Text { get; private set; } = null!;
    public bool Done { get; private set; }

    internal void Set(bool done) => Done = done;
}
