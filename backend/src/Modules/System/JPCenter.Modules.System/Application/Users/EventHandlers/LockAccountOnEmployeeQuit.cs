using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Hr.Contracts.IntegrationEvents;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.System.Application.Users.EventHandlers;

/// <summary>
/// PQ-08.E6 — nhân viên nghỉ việc (Hr) → khóa tài khoản, thu hồi mọi phiên ngay, nhật ký mức nghiêm trọng.
/// Chạy lại an toàn: không có tài khoản / đã khóa → bỏ qua. Super Admin cuối cùng → không khóa, ghi cảnh báo
/// (Hr đã chặn trước ở NS-07.E7 — đây là lưới an toàn khi quyền đổi giữa lúc hẹn ngày và lúc tới ngày).
/// </summary>
public sealed class LockAccountOnEmployeeQuit(
    ISystemDbContext db, SuperAdminGuard superAdmins, SessionRevoker sessions, PermissionChanges changes, IAuditTrail audit,
    ILogger<LockAccountOnEmployeeQuit> logger) : IIntegrationEventHandler<EmployeeQuit>
{
    public async Task Handle(EmployeeQuit e, CancellationToken ct)
    {
        var account = await db.UserAccounts.FirstOrDefaultAsync(a => a.EmployeeId == e.EmployeeId, ct);
        if (account is null || account.Status == UserAccountStatus.Locked) return;

        var roles = await changes.RolesAsync(ct);
        if (account.Status == UserAccountStatus.Active
            && SuperAdminGuard.HasSuperRole(account.Roles.Select(r => r.RoleId), roles)
            && await superAdmins.WouldLeaveNoneAsync(account.Id, ct))
        {
            logger.LogWarning("Không khóa tài khoản {Email} khi nghỉ việc: là Super Admin cuối cùng", account.Email);
            audit.Record(AuditCategory.Security, AuditSeverity.Critical,
                "Không khóa được tài khoản nghỉ việc", e.FullName, "Là Super Admin cuối cùng — cấp Super Admin cho người khác rồi khóa tay");
            return;
        }

        var reason = $"Đã nghỉ việc: {e.Reason}";
        account.Lock(reason.Length > 500 ? reason[..500] : reason);
        var revoked = await sessions.RevokeAllAsync(AccountKind.Staff, account.Id.Value, "account_locked", null, ct);
        await db.SaveChangesAsync(ct);
        changes.Invalidate(account.Id.Value);

        audit.Record(AuditCategory.Permission, AuditSeverity.Critical,
            "Khóa tài khoản (nghỉ việc)", e.FullName, $"{e.Reason} · nghỉ từ {e.QuitAt:dd/MM/yyyy} · thu hồi {revoked} phiên");
    }
}
