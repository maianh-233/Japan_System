using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// Thùng rác cho vai trò (PQ-04): khôi phục — trùng tên role khác thì thêm hậu tố "(khôi phục)" (PQ-04.E3);
/// xóa hẳn — xóa dòng <c>roles</c> (ma trận <c>role_grants</c> xóa theo khóa ngoại).
/// Role trong thùng rác không còn ai được gán (PQ-04.E2) nên khôi phục không đổi quyền của ai. Dùng chung DbContext với handler Thùng rác → khôi phục + bỏ mục lưu cùng 1 lần.
/// </summary>
internal sealed class RoleTrashHandler(SystemDbContext db) : ITrashHandler
{
    public const string RoleKind = "system.role";
    private const string RestoreSuffix = " (khôi phục)";

    public string Kind => RoleKind;

    public async Task<Result<TrashRestoreOutcome>> RestoreAsync(Guid entityId, CancellationToken ct)
    {
        var id = new RoleId(entityId);
        var role = await db.Roles.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter]).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return new TrashRestoreOutcome(false);
        if (!db.IsSoftDeleted(role)) return new TrashRestoreOutcome(true, "Vai trò đang hoạt động — không cần khôi phục");

        string? note = null;
        if (await db.Roles.AnyAsync(r => r.Name == role.Name, ct))
        {
            var name = role.Name + RestoreSuffix;
            for (var n = 2; await db.Roles.AnyAsync(r => r.Name == name, ct); n++) name = $"{role.Name} (khôi phục {n})";
            note = $"Đã có vai trò cùng tên — khôi phục với tên \"{name}\"";
            role.RenameForRestore(name);
        }

        db.Undelete(role);
        return new TrashRestoreOutcome(true, note);
    }

    public async Task PurgeAsync(Guid entityId, CancellationToken ct)
    {
        var id = new RoleId(entityId);
        await db.Roles.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter])
            .Where(r => r.Id == id && EF.Property<bool>(r, ShadowColumns.IsDeleted))
            .ExecuteDeleteAsync(ct);
    }
}
