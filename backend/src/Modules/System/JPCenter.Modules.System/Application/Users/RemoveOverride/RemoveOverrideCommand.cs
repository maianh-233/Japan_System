using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Users.Common;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Users.RemoveOverride;

/// <summary>PQ-06.E7 — gỡ quyền lẻ trước hạn.</summary>
public sealed record RemoveOverrideCommand(Guid AccountId, Guid OverrideId) : ICommand<UserChangeResult>;

/// <summary>
/// Gỡ cấp lẻ = thu hẹp → luôn được. Gỡ CHẶN lẻ = mở lại quyền → người gỡ phải có quyền đó (quy tắc vàng 1).
/// Mở lại quyền tài chính từ role không cần duyệt lại — role đó đã qua duyệt khi gán.
/// </summary>
public sealed class RemoveOverrideHandler(ISystemDbContext db, ICurrentUser user, UserEditGuard guard, PermissionChanges changes,
    IEmployeeDirectory employees, UserReader reader, IAuditTrail audit) : ICommandHandler<RemoveOverrideCommand, UserChangeResult>
{
    public async Task<Result<UserChangeResult>> Handle(RemoveOverrideCommand cmd, CancellationToken ct)
    {
        var editor = user.Access!;
        var target = await guard.LoadTargetAsync(cmd.AccountId, editor, ct);
        if (target.IsFailure) return target.Error;
        var account = target.Value;

        var item = account.Overrides.FirstOrDefault(o => o.Id == cmd.OverrideId);
        if (item is null) return PermissionErrors.OverrideNotFound;
        if (item.Effect == OverrideEffect.Deny && !new GrantChecker(editor).CanGrant(item.Permission, PermissionScope.Own))
            return PermissionErrors.ExceedsGranter;

        account.RemoveOverride(item.Id);
        await db.SaveChangesAsync(ct);
        changes.Invalidate(account.Id.Value);

        var name = (await employees.FindAsync(account.EmployeeId, ct))?.FullName ?? account.Email;
        audit.Record(AuditCategory.Permission, AuditSeverity.Warning, item.Effect == OverrideEffect.Deny ? "Gỡ chặn quyền lẻ" : "Gỡ quyền lẻ",
            name, $"{item.Permission} — lý do cấp ban đầu: {item.Reason}");
        return new UserChangeResult(await reader.GetAsync(account.Id, ct), null, null, []);
    }
}
