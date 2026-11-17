using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Contracts.IntegrationEvents;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Employees.ApplyScheduledQuits;

/// <summary>NS-07.E6 — job hằng ngày: tới ngày nghỉ việc đã hẹn → chuyển "đã nghỉ" + phát <see cref="EmployeeQuit"/> (System khóa tài khoản).</summary>
public sealed record ApplyScheduledQuitsCommand : ICommand<int>;

public sealed class ApplyScheduledQuitsHandler(IHrDbContext db, IIntegrationEventPublisher events, IAuditTrail audit, IClock clock)
    : ICommandHandler<ApplyScheduledQuitsCommand, int>
{
    public async Task<Result<int>> Handle(ApplyScheduledQuitsCommand cmd, CancellationToken ct)
    {
        var today = clock.Today;
        var scheduled = await db.Employees.Where(e => e.Status != EmployeeStatus.Quit && e.QuitAt != null && e.QuitAt <= today).ToListAsync(ct);
        foreach (var e in scheduled.Where(e => e.ApplyScheduledQuit(today)))
        {
            events.Publish(new EmployeeQuit(e.Id.Value, e.FullName, e.QuitAt!.Value, e.QuitReason ?? "Nghỉ việc theo lịch hẹn"));
            audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Nghỉ việc (theo lịch hẹn)", e.FullName, $"{e.Code} · {e.QuitReason}",
                branchId: e.BranchId.Value);
        }
        if (scheduled.Count > 0) await db.SaveChangesAsync(ct);
        return scheduled.Count;
    }
}
