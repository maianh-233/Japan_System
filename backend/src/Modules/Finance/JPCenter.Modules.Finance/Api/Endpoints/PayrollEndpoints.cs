using JPCenter.Api.Common.Auditing;
using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Payroll.AddPayAdjustment;
using JPCenter.Modules.Finance.Application.Payroll.GetMyPayroll;
using JPCenter.Modules.Finance.Application.Payroll.GetStaffPayroll;
using JPCenter.Modules.Finance.Application.Payroll.GetTeacherPayroll;
using JPCenter.Modules.Finance.Application.Payroll.LockPayroll;
using JPCenter.Modules.Finance.Application.Payroll.MarkPayrollPaid;
using JPCenter.Modules.Finance.Application.Payroll.RemovePayAdjustment;
using JPCenter.Modules.Finance.Application.Payroll.UnlockPayroll;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Finance.Api.Endpoints;

/// <summary>
/// TC-13, TC-14 — lương GV (<c>finance.teacher-payroll.*</c>) / lương NV (<c>finance.staff-payroll.*</c>): bảng lương tháng, điều chỉnh,
/// chốt / mở khóa / đã chi theo chi nhánh. Cổng GV: <c>GET portal/payroll?month=</c> — lương tạm tính của chính mình.
/// </summary>
public static class PayrollEndpoints
{
    public sealed record AdjustBody(string? Month, Guid EmployeeId, decimal Amount, string? Reason);

    public sealed record BranchesBody(List<Guid>? BranchIds);

    public static void Map(IEndpointRouteBuilder finance)
    {
        const string teacher = FinanceAccess.TeacherPayroll;
        const string staff = FinanceAccess.StaffPayroll;

        finance.MapGet("/payroll/teacher", async (string? month, Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetTeacherPayrollQuery(month, branchId), ct)).ToHttpResult())
            .RequirePermission(teacher, PermissionAction.View);
        finance.MapGet("/payroll/staff", async (string? month, Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetStaffPayrollQuery(month, branchId), ct)).ToHttpResult())
            .RequirePermission(staff, PermissionAction.View);

        // Điều chỉnh: GV → quyền lương GV, NV → quyền lương NV (kiểm lại trong handler theo người được điều chỉnh)
        finance.MapPost("/pay-adjustments", async (AdjustBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AddPayAdjustmentCommand(b.Month, b.EmployeeId, b.Amount, b.Reason), ct)).ToCreatedResult(a => $"/api/v1/finance/pay-adjustments/{a.Id}"))
            .RequireAnyPermission((teacher, PermissionAction.Update), (staff, PermissionAction.Update))
            .Audited("Thêm điều chỉnh lương", AuditCategory.Finance);
        finance.MapDelete("/pay-adjustments/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new RemovePayAdjustmentCommand(id), ct)).ToHttpResult())
            .RequireAnyPermission((teacher, PermissionAction.Update), (staff, PermissionAction.Update))
            .Audited("Xóa điều chỉnh lương", AuditCategory.Finance, AuditSeverity.Warning);

        // Chốt / mở khóa / đã chi — kind: teacher · staff; body { branchIds } (rỗng = mọi chi nhánh trong phạm vi)
        MapAction(finance, "lock", "Chốt bảng lương", AuditSeverity.Info,
            (k, m, b) => new LockPayrollCommand(k, m, b));
        MapAction(finance, "unlock", "Mở khóa bảng lương", AuditSeverity.Warning,
            (k, m, b) => new UnlockPayrollCommand(k, m, b));
        MapAction(finance, "paid", "Đã chi lương", AuditSeverity.Info,
            (k, m, b) => new MarkPayrollPaidCommand(k, m, b));

        finance.MapGet("/portal/payroll", async (string? month, ISender s, CancellationToken ct) =>
                (await s.Send(new GetMyPayrollQuery(month), ct)).ToHttpResult())
            .RequirePortal(Portal.Teacher);
    }

    private static void MapAction<TCommand>(IEndpointRouteBuilder finance, string action, string audit, AuditSeverity severity,
        Func<PayrollKind, string, IReadOnlyList<Guid>, TCommand> command)
        where TCommand : ICommand<Application.Payroll.Common.PayrollActionResult>
    {
        // 2 route riêng để khai báo đúng quyền của từng loại bảng lương
        foreach (var (kind, page) in new[] { (PayrollKind.Teacher, FinanceAccess.TeacherPayroll), (PayrollKind.Staff, FinanceAccess.StaffPayroll) })
        {
            finance.MapPost($"/payroll/{(kind == PayrollKind.Teacher ? "teacher" : "staff")}/{{month}}/{action}",
                    async (string month, BranchesBody? b, ISender s, CancellationToken ct) =>
                        (await s.Send(command(kind, month, b?.BranchIds ?? []), ct)).ToHttpResult())
                .RequirePermission(page, PermissionAction.Update)
                .Audited(audit, AuditCategory.Finance, severity);
        }
    }
}
