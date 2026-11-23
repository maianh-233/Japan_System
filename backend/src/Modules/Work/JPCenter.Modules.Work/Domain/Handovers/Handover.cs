using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Handovers;

/// <summary>
/// Bàn giao cuối ca (BG-01, BG-02): tiền mặt tại quầy (hệ thống tính / NV đếm / người nhận đếm lại), lead đang dở, việc tồn, sự cố.
/// Ca sau xác nhận: khớp cả 3 số → đã nhận, lệch → lệch tiền. Bảng: work.handovers (+ handover_leads, handover_items),
/// UQ (branch_id, date, from_shift, from_id)
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
    /// <summary>Hệ thống tính: quỹ đầu ca + tiền mặt thu trong ca (Finance)</summary>
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

    /// <summary>Người gửi / người nhận</summary>
    public bool Involves(Guid employeeId) => FromId == employeeId || ToId == employeeId;

    public static Result<Handover> Create(BranchId branchId, DateOnly date, ShiftKey fromShift, Guid fromId, Guid toId, Money cashExpected, Money cashCounted,
        string? cashNote, string? issues, IEnumerable<(Guid LeadId, string Note)> leads, IEnumerable<string> pending, DateTimeOffset at)
    {
        if (toId == fromId) return HandoverErrors.InvalidReceiver;
        if (cashCounted < Money.Zero || cashExpected < Money.Zero) return HandoverErrors.InvalidCash;
        if (cashCounted != cashExpected && string.IsNullOrWhiteSpace(cashNote)) return HandoverErrors.NoteRequired;
        var h = new Handover
        {
            Id = HandoverId.New(), BranchId = branchId, Date = date, FromShift = fromShift, FromId = fromId, ToId = toId, CashExpected = cashExpected,
            CashCounted = cashCounted, CashNote = Clean(cashNote), Issues = Clean(issues), HandedAt = at, Status = HandoverStatus.Pending,
        };
        h._leads.AddRange(leads.DistinctBy(l => l.LeadId).Select(l => new HandoverLead(l.LeadId, l.Note.Trim())));
        h._items.AddRange(pending.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => new HandoverItem(p.Trim())));
        return h;
    }

    /// <summary>BG-02 — người nhận đếm lại: khớp cả hệ thống / NV đếm / đếm lại → đã nhận; ngược lại lệch tiền</summary>
    public Result Receive(Guid by, Money counted, string? note, DateTimeOffset at)
    {
        if (by != ToId) return HandoverErrors.NotReceiver;
        if (Status != HandoverStatus.Pending) return HandoverErrors.Closed;
        if (counted < Money.Zero) return HandoverErrors.InvalidCash;
        (CashReceived, ReceiverNote, ReceivedAt) = (counted, Clean(note), at);
        Status = counted == CashCounted && CashCounted == CashExpected ? HandoverStatus.Received : HandoverStatus.Disputed;
        return Result.Success();
    }

    /// <summary>BG-02 — người nhận tick việc tồn đã xử lý (sau khi nhận)</summary>
    public Result TickItem(Guid by, Guid itemId, bool done)
    {
        if (by != ToId) return HandoverErrors.NotReceiver;
        if (Status == HandoverStatus.Pending) return HandoverErrors.NotReceived;
        if (_items.FirstOrDefault(i => i.Id == itemId) is not { } item) return HandoverErrors.ItemNotFound;
        item.Set(done);
        return Result.Success();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
