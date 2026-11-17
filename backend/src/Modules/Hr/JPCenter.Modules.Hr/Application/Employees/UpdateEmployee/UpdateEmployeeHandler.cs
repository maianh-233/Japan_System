using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Employees.UpdateEmployee;

/// <summary>
/// NS-04: tự quản lý mình (E1) · vòng quản lý (E2) · đổi email khi có tài khoản → cảnh báo (E3) · đổi chi nhánh → cảnh báo phân quyền (E4) ·
/// người đã nghỉ chỉ sửa SĐT + ghi chú (E6) · sửa đè (E7, xmin).
/// </summary>
public sealed class UpdateEmployeeHandler(IHrDbContext db, EmployeeRules rules, EmployeeAccess access, IAuditTrail audit)
    : ICommandHandler<UpdateEmployeeCommand, EmployeeResult>
{
    public async Task<Result<EmployeeResult>> Handle(UpdateEmployeeCommand cmd, CancellationToken ct)
    {
        var e = await access.FindForUpdateAsync(cmd.Id, cmd.Scope, ct);
        if (e is null) return EmployeeErrors.NotFound;
        ((DbContext)db).Entry(e).Property("xmin").OriginalValue = cmd.Version;
        var warnings = new List<string>();

        if (e.HasQuit)
        {
            e.UpdateContact(cmd.Phone, cmd.Note);
            warnings.Add("Người đã nghỉ việc — chỉ cập nhật số điện thoại và ghi chú.");
        }
        else
        {
            var branchChanged = e.BranchId.Value != cmd.BranchId;
            if (branchChanged)
            {
                if (!access.CanUseBranch(cmd.Scope, PermissionAction.Update, cmd.BranchId)) return EmployeeAccess.BranchNotAllowed;
                if (await rules.EnsureBranchAsync(cmd.BranchId, ct) is { IsFailure: true } branch) return branch.Error;
                warnings.Add("Đã đổi chi nhánh chính — vai trò & chi nhánh trong Phân quyền người dùng KHÔNG tự đổi, cập nhật nếu cần.");
            }
            if (await rules.EnsureEmailUniqueAsync(cmd.Email, e.Id, ct) is { IsFailure: true } dup) return dup.Error;
            if (e.HasAccount && !string.Equals(e.Email, cmd.Email.Trim(), StringComparison.OrdinalIgnoreCase))
                warnings.Add("Email đăng nhập vẫn là email cũ — đổi email đăng nhập ở tài khoản (Hệ thống).");
            if (await rules.EnsureManagerAsync(e, cmd.ManagerId, ct) is { IsFailure: true } mgr) return mgr.Error;

            if (e.Update(cmd.FullName, cmd.Gender, cmd.Dob, cmd.Phone, cmd.Email, new BranchId(cmd.BranchId), cmd.Department, cmd.Position,
                    cmd.ShiftBased, cmd.Note) is { IsFailure: true } invalid) return invalid.Error;
            if (e.ChangeStatus(cmd.Status) is { IsFailure: true } status) return status.Error;
            if (e.SetManager(cmd.ManagerId is { } m ? new EmployeeId(m) : null) is { IsFailure: true } self) return self.Error;
            if (e.IsTeaching && cmd.Teacher is { } t && e.SetTeacherProfile(EmployeeRules.Teacher(t, cmd.BranchId)) is { IsFailure: true } bad)
                return bad.Error;
        }

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Sửa hồ sơ nhân sự", e.FullName, e.Code, branchId: e.BranchId.Value);

        var all = await access.LoadAllAsync(ct);
        var self2 = all.Single(x => x.Employee.Id == e.Id);
        return new EmployeeResult(access.ToDto(self2.Employee, self2.Version, EmployeeAccess.Names(all), access.CanSeeSalary(cmd.Scope), true),
            warnings);
    }
}
