using JPCenter.Application.Common.Security;
using JPCenter.Application.Common.Time;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Application.Handovers.Common;

public sealed record HandoverCashDto(decimal Expected, decimal Counted, decimal? ReceivedCount, string? Note);

public sealed record HandoverLeadDto(Guid LeadId, string Note, string? Label);

public sealed record HandoverItemDto(Guid Id, string Text, bool Done);

/// <summary>Bàn giao ca — khớp <c>Handover</c> frontend (<c>createdAt</c> = giờ bàn giao); thêm quyền nhận / tick của người đang xem.</summary>
public sealed record HandoverDto(Guid Id, Guid BranchId, DateOnly Date, ShiftKey FromShift, Guid FromId, Guid ToId, HandoverCashDto Cash,
    IReadOnlyList<HandoverLeadDto> Leads, IReadOnlyList<HandoverItemDto> Pending, string? Issues, HandoverStatus Status, DateTimeOffset CreatedAt,
    DateTimeOffset? ReceivedAt, string? ReceiverNote, bool CanReceive);

/// <summary>
/// Quyền + tiền mặt hệ thống của bàn giao ca (quy tắc vàng 5): quỹ đầu ca + tiền mặt thu (trừ hoàn) của chi nhánh trong khung giờ ca, đọc từ Finance.
/// </summary>
public sealed class HandoverDesk(ICurrentUser user, WorkPeople people, ICashLedger cash, ILeadLookup leads)
{
    public const string Page = "work.handover";
    /// <summary>Quỹ đầu ca (BG-01 bước 2). ⏳ Chưa có trong Cài đặt chung — đưa vào khi cần sửa.</summary>
    public const decimal OpeningFloat = 1_000_000;

    /// <summary>Xem theo chi nhánh (phạm vi own = chỉ bàn giao mình gửi / nhận)</summary>
    public bool ViewsBranch(Guid branchId) =>
        user.Permission(Page, PermissionAction.View) is { Scope: not PermissionScope.Own } && user.InBranch(Page, PermissionAction.View, branchId);

    public bool CanSee(Handover h) => (people.MeId is { } me && h.Involves(me)) || ViewsBranch(h.BranchId.Value);

    /// <summary>Tiền mặt thu trong khung giờ ca (giờ VN) của chi nhánh, VND</summary>
    public async Task<decimal> CollectedAsync(Guid branchId, DateOnly date, ShiftKey shift, CancellationToken ct)
    {
        var (from, to, _) = WorkShiftTimes.Of(shift);
        return await cash.CashCollectedAsync(branchId, TimeText.At(date, from), TimeText.At(date, to), ct);
    }

    public async Task<IReadOnlyList<HandoverDto>> ToDtosAsync(IReadOnlyList<Handover> list, CancellationToken ct)
    {
        var leadIds = list.SelectMany(h => h.Leads.Select(l => l.LeadId)).Distinct().ToList();
        var found = leadIds.Count == 0 ? new Dictionary<Guid, Crm.Contracts.Dtos.LeadBrief>() : await leads.FindManyAsync(leadIds, ct);
        var me = people.MeId;
        return list.Select(h => new HandoverDto(h.Id.Value, h.BranchId.Value, h.Date, h.FromShift, h.FromId, h.ToId,
            new HandoverCashDto(h.CashExpected.Amount, h.CashCounted.Amount, h.CashReceived?.Amount, h.CashNote),
            h.Leads.Select(l => new HandoverLeadDto(l.LeadId, l.Note, found.TryGetValue(l.LeadId, out var b) ? $"{b.FullName} · {b.Phone}" : null)).ToList(),
            h.PendingItems.Select(i => new HandoverItemDto(i.Id, i.Text, i.Done)).ToList(), h.Issues, h.Status, h.HandedAt, h.ReceivedAt, h.ReceiverNote,
            h.Status == HandoverStatus.Pending && me == h.ToId)).ToList();
    }

    public async Task<HandoverDto> ToDtoAsync(Handover h, CancellationToken ct) => (await ToDtosAsync([h], ct))[0];
}
