using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Domain.Trash;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Trash.Common;

/// <summary>
/// Dùng chung cho các use case Thùng rác: mục được xem theo phạm vi <c>system.trash.view</c> (TR-01),
/// tìm <see cref="ITrashHandler"/> theo loại, dựng DTO (tên người xóa).
/// </summary>
public sealed class TrashBin(ISystemDbContext db, ICurrentUser user, IEnumerable<ITrashHandler> handlers, IEmployeeDirectory employees, IClock clock)
{
    public const string Page = "system.trash";
    private const string SystemName = "Hệ thống";

    private readonly Dictionary<string, ITrashHandler> _handlers = handlers.ToDictionary(h => h.Kind);

    public ITrashHandler? HandlerFor(string kind) => _handlers.GetValueOrDefault(kind);

    /// <summary>
    /// Toàn hệ thống → mọi mục · chi nhánh → mục của chi nhánh được gán (mục không gắn chi nhánh như vai trò: chỉ toàn hệ thống) ·
    /// của mình → mục mình xóa.
    /// </summary>
    public IQueryable<TrashItem> Visible()
    {
        var query = db.TrashItems.AsQueryable();
        var me = user.AccountId ?? Guid.Empty;
        return user.Permission(Page, PermissionAction.View) switch
        {
            { Scope: PermissionScope.All } => query,
            { Scope: PermissionScope.Branch, BranchIds: var ids } => Branches(query, ids.Select(id => new BranchId(id)).ToList()),
            { Scope: PermissionScope.Own } => query.Where(t => t.DeletedBy == me),
            _ => query.Where(_ => false),
        };
    }

    private static IQueryable<TrashItem> Branches(IQueryable<TrashItem> query, List<BranchId> ids) =>
        query.Where(t => t.BranchId != null && ids.Contains(t.BranchId.Value));

    public async Task<TrashItem?> FindAsync(Guid id, CancellationToken ct) =>
        await Visible().FirstOrDefaultAsync(t => t.Id == new TrashItemId(id), ct);

    public async Task<IReadOnlyList<TrashItemDto>> ToDtosAsync(IReadOnlyList<TrashItem> items, CancellationToken ct)
    {
        var accountIds = items.Select(i => i.DeletedBy).Where(id => id != Guid.Empty).Distinct().Select(id => new UserAccountId(id)).ToList();
        var accounts = (await db.UserAccounts.AsNoTracking().Where(a => accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.EmployeeId }).ToListAsync(ct)).Select(a => new { Id = a.Id.Value, a.EmployeeId }).ToList();
        var names = await employees.FindManyAsync(accounts.Select(a => a.EmployeeId).Distinct().ToList(), ct);
        var byAccount = accounts.ToDictionary(a => a.Id, a => names.TryGetValue(a.EmployeeId, out var e) ? e.FullName : SystemName);

        var now = clock.UtcNow;
        return items.Select(i => new TrashItemDto(i.Id.Value, i.Kind, i.Label, i.Detail, i.BranchId?.Value, i.DeletedAt,
            byAccount.GetValueOrDefault(i.DeletedBy, SystemName), i.PurgeAfter, i.DaysLeft(now), _handlers.ContainsKey(i.Kind))).ToList();
    }
}
