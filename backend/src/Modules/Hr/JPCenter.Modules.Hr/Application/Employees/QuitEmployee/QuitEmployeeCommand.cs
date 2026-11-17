using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Contracts.IntegrationEvents;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Employees.QuitEmployee;

/// <summary>NS-07 — cho nghỉ việc. Lý do bắt buộc; ngày nghỉ bỏ trống = hôm nay, ở tương lai = hẹn ngày (E6).</summary>
public sealed record QuitEmployeeCommand(EmployeeGroup Scope, Guid Id, string Reason, DateOnly? QuitAt) : ICommand<EmployeeResult>;

public sealed class QuitEmployeeValidator : AbstractValidator<QuitEmployeeCommand>
{
    public QuitEmployeeValidator() =>
        RuleFor(x => x.Reason).NotEmpty().WithErrorCode("hr.employee.quit_reason_required").WithMessage("Nhập lý do").MaximumLength(500);
}

/// <summary>
/// Đã nghỉ (E1) · Super Admin cuối cùng → chặn (E7) · đang quản lý người khác / quản lý chi nhánh → cảnh báo (E3, E4).
/// Nghỉ ngay → phát <see cref="EmployeeQuit"/> (System khóa tài khoản + thu hồi phiên). ⏳ E2, E5 (lớp đang dạy, lead / task) — chờ module khác.
/// </summary>
public sealed class QuitEmployeeHandler(
    IHrDbContext db, EmployeeAccess access, IStaffAccessDirectory staffAccess, IBranchDirectory branches, IIntegrationEventPublisher events,
    IAuditTrail audit, IClock clock) : ICommandHandler<QuitEmployeeCommand, EmployeeResult>
{
    public async Task<Result<EmployeeResult>> Handle(QuitEmployeeCommand cmd, CancellationToken ct)
    {
        var e = await access.FindForUpdateAsync(cmd.Id, cmd.Scope, ct);
        if (e is null) return EmployeeErrors.NotFound;
        if (e.HasQuit) return EmployeeErrors.AlreadyQuit;
        if (await staffAccess.IsLastActiveSuperAdminAsync(e.Id.Value, ct)) return EmployeeErrors.LastSuperAdmin;

        var today = clock.Today;
        var quit = e.Quit(cmd.QuitAt ?? today, cmd.Reason, today);
        if (quit.IsFailure) return quit.Error;
        var effective = quit.Value;

        var warnings = new List<string>();
        var reports = await db.Employees.CountAsync(x => x.ManagerId == e.Id && x.Status != EmployeeStatus.Quit, ct);
        if (reports > 0) warnings.Add($"{reports} người đang báo cáo cho {e.FullName} — đổi quản lý trực tiếp cho họ.");
        var managed = (await branches.ListAsync(false, ct)).Where(b => b.ManagerId == e.Id.Value).Select(b => b.Name).ToList();
        if (managed.Count > 0) warnings.Add($"Đang là quản lý chi nhánh {string.Join(", ", managed)} — chọn quản lý mới ở Hệ thống › Chi nhánh.");
        if (!effective) warnings.Add($"Đã hẹn nghỉ việc ngày {e.QuitAt:dd/MM/yyyy} — tới ngày hệ thống tự chuyển trạng thái và khóa tài khoản.");

        if (effective) events.Publish(new EmployeeQuit(e.Id.Value, e.FullName, e.QuitAt!.Value, e.QuitReason!));
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, effective ? "Cho nghỉ việc" : "Hẹn nghỉ việc", e.FullName,
            $"{e.Code} · từ {e.QuitAt:dd/MM/yyyy} · {e.QuitReason}", branchId: e.BranchId.Value);
        return new EmployeeResult(await access.DetailAsync(e.Id, cmd.Scope, ct), warnings);
    }
}
