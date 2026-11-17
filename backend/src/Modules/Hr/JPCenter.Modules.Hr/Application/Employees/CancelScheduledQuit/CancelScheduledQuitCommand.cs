using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.CancelScheduledQuit;

/// <summary>NS-07.E9 — hủy lịch hẹn nghỉ việc chưa tới ngày.</summary>
public sealed record CancelScheduledQuitCommand(EmployeeGroup Scope, Guid Id) : ICommand<EmployeeDto>;

public sealed class CancelScheduledQuitHandler(IHrDbContext db, EmployeeAccess access, IAuditTrail audit)
    : ICommandHandler<CancelScheduledQuitCommand, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> Handle(CancelScheduledQuitCommand cmd, CancellationToken ct)
    {
        var e = await access.FindForUpdateAsync(cmd.Id, cmd.Scope, ct);
        if (e is null) return EmployeeErrors.NotFound;
        var at = e.QuitAt;
        if (e.CancelScheduledQuit() is { IsFailure: true } failed) return failed.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Hủy hẹn nghỉ việc", e.FullName, $"{e.Code} · đã hẹn {at:dd/MM/yyyy}",
            branchId: e.BranchId.Value);
        return await access.DetailAsync(e.Id, cmd.Scope, ct);
    }
}
