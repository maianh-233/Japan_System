using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Handovers;

/// <summary>
/// Bàn giao cuối ca: tiền mặt tại quầy (hệ thống tính / NV đếm / người nhận đếm lại), lead đang dở, việc tồn.
/// Ca sau xác nhận nhận hoặc khiếu nại. Bảng: work.handovers (+ handover_leads, handover_items)
/// </summary>
public sealed class Handover : AggregateRoot<HandoverId>, IBranchScoped, IAuditable
{
    private readonly List<HandoverLead> _leads = [];
    private readonly List<HandoverItem> _items = [];

    private Handover() { }

    public BranchId BranchId { get; private set; }
    public DateOnly Date { get; private set; }
    public ShiftKey FromShift { get; private set; }
    /// <summary>Người bàn giao (Hr.Employee)</summary>
    public Guid FromId { get; private set; }
    /// <summary>Người nhận (Hr.Employee)</summary>
    public Guid ToId { get; private set; }
    /// <summary>Hệ thống tính: tồn đầu ca + tiền mặt thu trong ca</summary>
    public Money CashExpected { get; private set; }
    /// <summary>NV đếm thực tế</summary>
    public Money CashCounted { get; private set; }
    /// <summary>Người nhận đếm lại</summary>
    public Money? CashReceived { get; private set; }
    public string? CashNote { get; private set; }
    public string? Issues { get; private set; }
    public HandoverStatus Status { get; private set; }
    public DateTimeOffset HandedAt { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public string? ReceiverNote { get; private set; }
    public IReadOnlyList<HandoverLead> Leads => _leads;
    public IReadOnlyList<HandoverItem> PendingItems => _items;

    public static Handover Create(BranchId branchId, DateOnly date, ShiftKey fromShift, Guid fromId, Guid toId, Money cashExpected, Money cashCounted,
        string? cashNote, string? issues, IEnumerable<(Guid LeadId, string Note)> leads, IEnumerable<string> pending, DateTimeOffset at)
    {
        var h = new Handover
        {
            Id = HandoverId.New(), BranchId = branchId, Date = date, FromShift = fromShift, FromId = fromId, ToId = toId, CashExpected = cashExpected,
            CashCounted = cashCounted, CashNote = cashNote, Issues = issues, HandedAt = at, Status = HandoverStatus.Pending,
        };
        h._leads.AddRange(leads.Select(l => new HandoverLead(l.LeadId, l.Note)));
        h._items.AddRange(pending.Select(p => new HandoverItem(p)));
        return h;
    }

    public Result Receive(Guid by, Money counted, string? note, DateTimeOffset at)
    {
        if (by != ToId) return Error.Forbidden("work.handover.not_receiver", "Chỉ người nhận ca được xác nhận");
        if (Status != HandoverStatus.Pending) return Error.Conflict("work.handover.closed", "Bàn giao đã được xác nhận");
        (CashReceived, ReceiverNote, ReceivedAt) = (counted, note, at);
        Status = counted == CashCounted ? HandoverStatus.Received : HandoverStatus.Disputed;
        return Result.Success();
    }

    public void TickItem(Guid itemId, bool done) => _items.FirstOrDefault(i => i.Id == itemId)?.Set(done);
}
