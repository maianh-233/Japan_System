using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.CreateEmployee;

/// <summary>NS-03: email trùng (E3) · chi nhánh đã đóng (E7) · quản lý không hợp lệ (E9) · hợp đồng sai (E4, E5) · chi nhánh dạy thêm trùng chính (E8).</summary>
public sealed class CreateEmployeeHandler(IHrDbContext db, EmployeeRules rules, EmployeeAccess access, IAuditTrail audit)
    : ICommandHandler<CreateEmployeeCommand, EmployeeDto>
{
    public const decimal DefaultLeaveDays = 12;

    public async Task<Result<EmployeeDto>> Handle(CreateEmployeeCommand cmd, CancellationToken ct)
    {
        if (!access.CanUseBranch(cmd.Scope, global::JPCenter.Application.Common.Security.PermissionAction.Create, cmd.BranchId))
            return EmployeeAccess.BranchNotAllowed;
        if (await rules.EnsureEmailUniqueAsync(cmd.Email, null, ct) is { IsFailure: true } dup) return dup.Error;
        if (await rules.EnsureBranchAsync(cmd.BranchId, ct) is { IsFailure: true } branch) return branch.Error;
        if (await rules.EnsureManagerAsync(null, cmd.ManagerId, ct) is { IsFailure: true } mgr) return mgr.Error;

        var code = await rules.NextCodeAsync(cmd.Kind, ct);
        var contract = EmployeeRules.Contract(cmd.Contract, EmployeeRules.ContractNo(code, cmd.Contract.Start.Year, 1));
        if (contract.IsFailure) return contract.Error;

        var created = Employee.Create(code, cmd.FullName, cmd.Gender, cmd.Dob, cmd.Phone, cmd.Email, new BranchId(cmd.BranchId), cmd.Kind,
            cmd.Department, cmd.Position, cmd.ShiftBased, cmd.Contract.Start, cmd.ManagerId is { } m ? new EmployeeId(m) : null,
            contract.Value, DefaultLeaveDays, cmd.Status);
        if (created.IsFailure) return created.Error;
        var employee = created.Value;
        employee.UpdateContact(employee.Phone, cmd.Note);
        if (cmd.Teacher is { } t && employee.SetTeacherProfile(EmployeeRules.Teacher(t, cmd.BranchId)) is { IsFailure: true } bad) return bad.Error;

        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, $"Thêm {KindText(employee.Kind)}", employee.FullName,
            $"{employee.Code} · {employee.Position}", branchId: employee.BranchId.Value);

        var all = await access.LoadAllAsync(ct);
        var self = all.Single(x => x.Employee.Id == employee.Id);
        return access.ToDto(self.Employee, self.Version, EmployeeAccess.Names(all), access.CanSeeSalary(cmd.Scope), withHistory: true);
    }

    public static string KindText(EmployeeKind kind) => kind switch
    {
        EmployeeKind.Teacher => "giáo viên",
        EmployeeKind.Assistant => "trợ giảng",
        _ => "nhân viên",
    };
}
