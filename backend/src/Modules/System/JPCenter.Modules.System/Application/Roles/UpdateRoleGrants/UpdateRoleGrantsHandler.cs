using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.Modules.System.Application.Permissions.Common;
using JPCenter.Modules.System.Application.Roles.Common;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Roles.UpdateRoleGrants;

/// <summary>
/// PQ-03. Ô vượt quá quyền người sửa → giữ giá trị cũ (E2). Tự thêm Xem (E4). Super Admin không sửa (E1).
/// Thêm / nới quyền <c>finance.*</c> cho role ĐANG CÓ người dùng → phần đó thành yêu cầu duyệt 2 người, phần còn lại lưu ngay (E5).
/// Quyền mới áp dụng ngay cho mọi người dùng role (xóa cache quyền).
/// </summary>
public sealed class UpdateRoleGrantsHandler(
    ISystemDbContext db, ICurrentUser user, IPermissionCatalog catalog, PermissionChanges changes, ApprovalFactory approvals,
    RoleReader reader, IAuditTrail audit) : ICommandHandler<UpdateRoleGrantsCommand, RoleSaveResult>
{
    public async Task<Result<RoleSaveResult>> Handle(UpdateRoleGrantsCommand cmd, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == new RoleId(cmd.RoleId), ct);
        if (role is null) return PermissionErrors.RoleNotFound;
        if (role.IsSuperAdmin) return RoleErrors.SuperAdminLocked;
        if (cmd.Grants.Keys.FirstOrDefault(p => !catalog.IsValid(p)) is not null) return PermissionErrors.UnknownPermission;

        // PQ-03.E6: chống sửa đè — version cũ → DbUpdateConcurrencyException → 409 system.concurrency.
        // Ma trận nằm ở bảng con (role_grants) → ép cập nhật dòng roles để PostgreSQL kiểm xmin và tăng version.
        var entry = ((DbContext)db).Entry(role);
        entry.Property("xmin").OriginalValue = cmd.Version;
        entry.State = EntityState.Modified;

        var checker = new GrantChecker(user.Access!);
        var old = role.Grants.ToDictionary(g => g.Permission, g => g.Scope);
        var wanted = PermissionChanges.WithImpliedView(cmd.Grants.ToDictionary(kv => kv.Key, kv => RoleGrants.ParseScope(kv.Value)), catalog);

        var cells = new Dictionary<string, string?>();
        var kept = new List<string>();
        foreach (var perm in old.Keys.Union(wanted.Keys))
        {
            var before = old.TryGetValue(perm, out var b) ? b : (PermissionScope?)null;
            var after = wanted.TryGetValue(perm, out var a) ? a : (PermissionScope?)null;
            if (before == after) continue;
            // Chỉ đổi được ô mà người sửa có quyền ở mức cao hơn của cả trước lẫn sau
            var highest = (PermissionScope)Math.Max((int)(before ?? PermissionScope.Own), (int)(after ?? PermissionScope.Own));
            if (!checker.CanGrant(perm, highest)) { kept.Add(perm); continue; }
            cells[perm] = after is null ? null : RoleGrants.ScopeText(after.Value);
        }

        // PQ-03.E5: role đang có người dùng + thêm / nới quyền tài chính → tách ra chờ duyệt
        var hasUsers = await db.UserAccounts.AnyAsync(a => a.Roles.Any(r => r.RoleId == role.Id), ct);
        var financeCells = hasUsers
            ? cells.Where(c => RoleGrants.IsFinance(c.Key) && c.Value is not null
                && (!old.TryGetValue(c.Key, out var o) || RoleGrants.ParseScope(c.Value) > o)).ToDictionary(c => c.Key, c => c.Value!)
            : [];
        foreach (var perm in financeCells.Keys) cells.Remove(perm);

        if (cells.Count > 0 && PermissionChanges.MergeGrants(role, cells) is { IsFailure: true } failed) return failed.Error;

        Guid? approvalId = null;
        if (financeCells.Count > 0)
        {
            var request = await approvals.FinancePermissionAsync(user.Access!, $"Cấp quyền tài chính cho vai trò {role.Name}",
                string.Join(", ", financeCells.Select(c => $"{c.Key} → {c.Value}")),
                new FinancePermissionPayload(FinancePermissionPayload.RoleGrants, null, null, null, null, role.Id.Value, role.Name,
                    financeCells, financeCells.Keys.Order().ToList(), null, null), ct);
            approvalId = request.Id.Value;
        }

        await db.SaveChangesAsync(ct);
        changes.Invalidate();

        if (cells.Count > 0)
        {
            audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Sửa quyền vai trò", role.Name,
                string.Join("; ", cells.Select(c => $"{c.Key}: {(old.TryGetValue(c.Key, out var o) ? RoleGrants.ScopeText(o) : "—")} → {c.Value ?? "—"}")));
        }
        if (approvalId is not null)
            audit.Record(AuditCategory.Permission, AuditSeverity.Warning, "Gửi duyệt cấp quyền tài chính", role.Name, string.Join(", ", financeCells.Keys));

        var message = string.Join(" ", new[]
        {
            kept.Count > 0 ? $"{kept.Count} quyền vượt quá quyền của bạn — giữ nguyên." : null,
            approvalId is not null ? "Phần quyền tài chính đã gửi yêu cầu duyệt." : null,
        }.OfType<string>());
        return new RoleSaveResult(await reader.GetAsync(role.Id, ct), kept, approvalId, message.Length > 0 ? message : null);
    }
}
