using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.SetLongLeave;

/// <summary>NS-10 — nghỉ dài hạn (<paramref name="OnLeave"/> = true) / quay lại làm việc. Tài khoản KHÔNG bị khóa (E1). Người đã nghỉ việc → không cho (E2).</summary>
public sealed record SetLongLeaveCommand(EmployeeGroup Scope, Guid Id, bool OnLeave) : ICommand<EmployeeDto>;

public sealed class SetLongLeaveHandler(IHrDbContext db, EmployeeAccess access, IAuditTrail audit) : ICommandHandler<SetLongLeaveCommand, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> Handle(SetLongLeaveCommand cmd, CancellationToken ct)
    {
        var e = await access.FindForUpdateAsync(cmd.Id, cmd.Scope, ct);
        if (e is null) return EmployeeErrors.NotFound;
        if (e.HasQuit) return EmployeeErrors.AlreadyQuit;
        if ((cmd.OnLeave ? e.StartLongLeave() : e.ReturnFromLeave()) is { IsFailure: true } failed) return failed.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.OnLeave ? "Nghỉ dài hạn" : "Quay lại làm việc", e.FullName, e.Code,
            branchId: e.BranchId.Value);
        return await access.DetailAsync(e.Id, cmd.Scope, ct);
    }
}
