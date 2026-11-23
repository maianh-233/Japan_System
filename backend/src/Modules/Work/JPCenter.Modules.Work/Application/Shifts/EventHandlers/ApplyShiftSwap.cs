using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Hr.Contracts.IntegrationEvents;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Domain.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Work.Application.Shifts.EventHandlers;

/// <summary>
/// XC-05 — đơn đổi ca đã duyệt (Hr): 2 ca đổi người cho nhau, 2 người nhận thông báo lịch mới. Một trong 2 ca không còn / đã đổi người → bỏ qua + log
/// (Hr đã kiểm lúc duyệt). Chạy lại: ca đã mang người mới → coi như đã xong.
/// </summary>
public sealed class ApplyShiftSwap(IWorkDbContext db, WorkNotices notices, ILogger<ApplyShiftSwap> logger) : IIntegrationEventHandler<ShiftSwapApproved>
{
    public async Task Handle(ShiftSwapApproved e, CancellationToken ct)
    {
        var ids = new[] { new ShiftAssignmentId(e.RequesterShiftId), new ShiftAssignmentId(e.ColleagueShiftId) };
        var shifts = await db.ShiftAssignments.Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        var mine = shifts.FirstOrDefault(a => a.Id.Value == e.RequesterShiftId);
        var theirs = shifts.FirstOrDefault(a => a.Id.Value == e.ColleagueShiftId);
        if (mine is null || theirs is null)
        {
            logger.LogWarning("Đổi ca {Code}: ca không còn — bỏ qua", e.Code);
            return;
        }
        if (mine.EmployeeId == e.ColleagueId && theirs.EmployeeId == e.RequesterId) return; // đã đổi (chạy lại)
        if (mine.EmployeeId != e.RequesterId || theirs.EmployeeId != e.ColleagueId)
        {
            logger.LogWarning("Đổi ca {Code}: ca đã đổi người trước đó — bỏ qua", e.Code);
            return;
        }

        // Tránh trùng khóa (employee, date, shift) khi 2 người đã có ca cùng ô bên kia
        var clash = await db.ShiftAssignments.AnyAsync(a =>
            (a.EmployeeId == e.ColleagueId && a.Date == mine.Date && a.Shift == mine.Shift && a.Id != theirs.Id)
            || (a.EmployeeId == e.RequesterId && a.Date == theirs.Date && a.Shift == theirs.Shift && a.Id != mine.Id), ct);
        if (clash)
        {
            logger.LogWarning("Đổi ca {Code}: người nhận đã có ca trùng ô — bỏ qua", e.Code);
            return;
        }

        mine.SwapTo(e.ColleagueId);
        theirs.SwapTo(e.RequesterId);
        var (a, b) = (Label(mine), Label(theirs));
        notices.ToEmployees([e.RequesterId], "Lịch ca mới (đổi ca)", $"Đơn {e.Code}: bạn nhận ca {b}, giao ca {a}", "info", WorkNotices.ShiftsPage,
            "work.shift.swapped");
        notices.ToEmployees([e.ColleagueId], "Lịch ca mới (đổi ca)", $"Đơn {e.Code}: bạn nhận ca {a}, giao ca {b}", "info", WorkNotices.ShiftsPage,
            "work.shift.swapped");
        await db.SaveChangesAsync(ct);
    }

    private static string Label(ShiftAssignment s) => $"{WorkShiftTimes.Of(s.Shift).Short} {s.Date:dd/MM}";
}
