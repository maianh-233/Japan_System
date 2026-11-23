using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Handovers.Common;
using JPCenter.Modules.Work.Application.Shifts.Common;
using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Handovers.GetHandoverDraft;

public sealed record ReceiverOptionDto(Guid Id, string FullName, IReadOnlyList<ShiftKey> ShiftsToday);

public sealed record OpenLeadDto(Guid Id, string Code, string FullName, string Phone, string Status);

/// <summary>
/// BG-01 bước 1–2 — gợi ý: ca của tôi (ca đang diễn ra, không thì ca gần nhất hôm nay), người nhận = NV có ca kế tiếp cùng chi nhánh,
/// tiền mặt hệ thống (quỹ đầu ca + thu trong ca), lead đang mở của chi nhánh. <c>handedShifts</c>: ca hôm nay tôi đã bàn giao.
/// </summary>
public sealed record HandoverDraftDto(DateOnly Date, ShiftKey Shift, IReadOnlyList<ShiftKey> MyShifts, IReadOnlyList<ShiftKey> HandedShifts,
    decimal OpeningFloat, decimal CashCollected, decimal CashExpected, Guid? ReceiverId, IReadOnlyList<ReceiverOptionDto> Receivers,
    IReadOnlyList<OpenLeadDto> Leads);

/// <param name="Shift">Ca muốn bàn giao (bỏ trống = hệ thống gợi ý)</param>
public sealed record GetHandoverDraftQuery(ShiftKey? Shift) : IQuery<HandoverDraftDto>;

public sealed class GetHandoverDraftHandler(IWorkDbContext db, WorkPeople people, HandoverDesk desk, ShiftDesk shifts, ILeadLookup leads, IClock clock)
    : IQueryHandler<GetHandoverDraftQuery, HandoverDraftDto>
{
    public const int LeadLimit = 50;

    public async Task<Result<HandoverDraftDto>> Handle(GetHandoverDraftQuery q, CancellationToken ct)
    {
        var me = await people.MeAsync(ct);
        if (!ShiftDesk.Assignable(me)) return ShiftErrors.NotShiftBased;
        var today = clock.Today;
        var now = TimeOnly.FromDateTime(clock.LocalNow);
        var branch = me!.BranchId;

        var staff = await shifts.ShiftStaffAsync(branch, ct);
        var ids = staff.Select(e => e.Id).ToList();
        var todayShifts = await db.ShiftAssignments.AsNoTracking().Where(a => ids.Contains(a.EmployeeId) && a.Date == today)
            .Select(a => new { a.EmployeeId, a.Shift }).ToListAsync(ct);
        var mine = todayShifts.Where(a => a.EmployeeId == me.Id).Select(a => a.Shift).OrderBy(s => s).ToList();

        var shift = q.Shift ?? Suggest(mine, now);
        var handed = await db.Handovers.AsNoTracking().Where(h => h.FromId == me.Id && h.Date == today).Select(h => h.FromShift).ToListAsync(ct);
        var collected = await desk.CollectedAsync(branch, today, shift, ct);

        var receivers = staff.Where(e => e.Id != me.Id && ShiftDesk.Assignable(e))
            .Select(e => new ReceiverOptionDto(e.Id, e.FullName, todayShifts.Where(a => a.EmployeeId == e.Id).Select(a => a.Shift).OrderBy(s => s).ToList()))
            .ToList();
        var next = WorkShiftTimes.Next(shift);
        var suggested = next is { } n ? receivers.FirstOrDefault(r => r.ShiftsToday.Contains(n))?.Id : null;

        var open = (await leads.OpenOfBranchAsync(branch, LeadLimit, ct))
            .Select(l => new OpenLeadDto(l.Id, l.Code, l.FullName, l.Phone, l.Status)).ToList();
        return new HandoverDraftDto(today, shift, mine, handed, HandoverDesk.OpeningFloat, collected, HandoverDesk.OpeningFloat + collected, suggested,
            receivers, open);
    }

    /// <summary>Ca đang diễn ra trong số ca của tôi; không có → ca muộn nhất đã bắt đầu; không có ca → ca theo giờ hiện tại</summary>
    public static ShiftKey Suggest(IReadOnlyList<ShiftKey> mine, TimeOnly now)
    {
        bool Started(ShiftKey s) => WorkShiftTimes.Of(s).From <= now;
        bool Running(ShiftKey s) => Started(s) && now < WorkShiftTimes.Of(s).To;
        if (mine.Where(Running).ToList() is { Count: > 0 } runningMine) return runningMine.Max();
        if (mine.Where(Started).ToList() is { Count: > 0 } startedMine) return startedMine.Max();
        if (mine.Count > 0) return mine.Min();
        var started = ShiftDesk.Shifts.Where(Started).ToList();
        return started.Count > 0 ? started.Max() : ShiftKey.Morning;
    }
}
