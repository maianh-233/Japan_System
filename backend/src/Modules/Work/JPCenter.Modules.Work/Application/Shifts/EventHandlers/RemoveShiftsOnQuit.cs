using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Hr.Contracts.IntegrationEvents;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Work.Application.Shifts.EventHandlers;

/// <summary>XC-05 — nhân viên nghỉ việc (Hr): gỡ ca + đăng ký rảnh / bận từ hôm nay trở đi (ca đã qua giữ để đối chiếu công). Chạy lại an toàn.</summary>
public sealed class RemoveShiftsOnQuit(IWorkDbContext db, IClock clock, ILogger<RemoveShiftsOnQuit> logger) : IIntegrationEventHandler<EmployeeQuit>
{
    public async Task Handle(EmployeeQuit e, CancellationToken ct)
    {
        var today = clock.Today;
        var shifts = await db.ShiftAssignments.Where(a => a.EmployeeId == e.EmployeeId && a.Date >= today).ToListAsync(ct);
        var regs = await db.ShiftRegistrations.Where(r => r.EmployeeId == e.EmployeeId && r.Date >= today).ToListAsync(ct);
        if (shifts.Count == 0 && regs.Count == 0) return;
        db.ShiftAssignments.RemoveRange(shifts);
        db.ShiftRegistrations.RemoveRange(regs);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Gỡ {Count} ca từ {Today} của {Employee} (nghỉ việc)", shifts.Count, today, e.FullName);
    }
}
