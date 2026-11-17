using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Users.Common;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Users.AddOverride;

/// <summary>
/// PQ-06: cấp vượt quá người cấp (E1) · chặn luôn được (E2) · cấp quyền tài chính → duyệt 2 người (E3) ·
/// hạn ở quá khứ (E4) · đã có quyền qua role → vẫn lưu, cảnh báo (E6).
/// </summary>
public sealed class AddOverrideHandler(
    ISystemDbContext db, ICurrentUser user, UserEditGuard guard, PermissionChanges changes, ApprovalFactory approvals,
    IEmployeeDirectory employees, UserReader reader, IAuditTrail audit, IClock clock)
    : ICommandHandler<AddOverrideCommand, UserChangeResult>
{
    public async Task<Result<UserChangeResult>> Handle(AddOverrideCommand cmd, CancellationToken ct)
    {
        var editor = user.Access!;
        var target = await guard.LoadTargetAsync(cmd.AccountId, editor, ct);
        if (target.IsFailure) return target.Error;
        var account = target.Value;

        var input = new OverrideInput(cmd.Perm, cmd.Effect, cmd.Effect == "grant" ? cmd.Scope : null, cmd.Until, cmd.Reason.Trim());
        var now = clock.UtcNow;
        if (guard.CheckOverride(input, editor, now) is { IsFailure: true } denied) return denied.Error;

        var roles = await changes.RolesAsync(ct);
        var name = (await employees.FindAsync(account.EmployeeId, ct))?.FullName ?? account.Email;
        var text = Describe(input);

        // PQ-06.E6: đã có quyền này từ role
        var warnings = new List<string>();
        if (input.Effect == "grant")
        {
            var current = EffectivePermissions.Compute(account.Roles, roles, [], now);
            if (current.TryGetValue(input.Permission, out var had) && had.Scope is not null)
                warnings.Add($"Quyền đã có từ vai trò {string.Join(", ", had.Sources.Distinct())} ({RoleGrants.ScopeText(had.Scope.Value)}) — lấy phạm vi rộng nhất.");
        }

        // PQ-06.E3: cấp lẻ làm có thêm quyền tài chính → chờ duyệt
        if (input.Effect == "grant" && RoleGrants.IsFinance(input.Permission))
        {
            var preview = new PermissionOverride(input.Permission, OverrideEffect.Grant, RoleGrants.ParseScope(input.Scope!), input.Until,
                input.Reason, editor.AccountId, now);
            var added = changes.FinanceAdded(account, roles, account.Roles, account.Overrides.Append(preview));
            if (added.Count > 0)
            {
                var request = await approvals.FinancePermissionAsync(editor, $"Cấp lẻ quyền tài chính cho {name}", $"{text} — {input.Reason}",
                    new FinancePermissionPayload(FinancePermissionPayload.UserOverride, account.Id.Value, name, null, input, null, null, null,
                        added.ToList(), null, text), ct);
                await db.SaveChangesAsync(ct);
                audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Gửi duyệt cấp lẻ quyền tài chính", name, text);
                return new UserChangeResult(await reader.GetAsync(account.Id, ct), request.Id.Value,
                    "Đã gửi yêu cầu duyệt — quyền tài chính chỉ áp dụng khi người thứ 2 duyệt.", warnings);
            }
        }

        changes.ApplyOverride(account, input, editor.AccountId);
        await db.SaveChangesAsync(ct);
        changes.Invalidate(account.Id.Value);
        audit.Record(AuditCategory.Permission, AuditSeverity.Warning, input.Effect == "deny" ? "Chặn quyền lẻ" : "Cấp quyền lẻ", name,
            $"{text} — {input.Reason}");
        return new UserChangeResult(await reader.GetAsync(account.Id, ct), null, null, warnings);
    }

    public static string Describe(OverrideInput input) =>
        $"{(input.Effect == "deny" ? "Chặn" : "Cấp")} {input.Permission}{(input.Scope is { } s ? $" ({s})" : "")}" +
        $"{(input.Until is { } u ? $" đến {u.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm}" : " vô thời hạn")}";
}
