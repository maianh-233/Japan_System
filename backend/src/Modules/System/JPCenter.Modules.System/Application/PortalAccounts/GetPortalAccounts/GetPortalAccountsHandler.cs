using JPCenter.Application.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.PortalAccounts.Common;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.PortalAccounts.GetPortalAccounts;

/// <summary>Tài khoản cổng của HV / PH trong phạm vi chi nhánh (PH cần thêm quyền xem Liên hệ)</summary>
public sealed class GetPortalAccountsHandler(ISystemDbContext db, PortalOwnerAccess owners) : IQueryHandler<GetPortalAccountsQuery, IReadOnlyList<PortalAccountDto>>
{
    public async Task<Result<IReadOnlyList<PortalAccountDto>>> Handle(GetPortalAccountsQuery query, CancellationToken ct)
    {
        var q = db.PortalAccounts.AsNoTracking();
        if (query.OwnerType is "student") q = q.Where(x => x.OwnerType == PortalOwnerType.Student);
        if (query.OwnerType is "guardian") q = q.Where(x => x.OwnerType == PortalOwnerType.Guardian);
        if (query.OwnerId is { } owner) q = q.Where(x => x.OwnerId == owner);

        var list = await q.OrderBy(x => x.Username).ToListAsync(ct);
        var visible = await owners.VisibleAsync(list.Select(a => (a.OwnerType, a.OwnerId)).ToList(), PermissionAction.View, ct);
        return list.Where(a => visible.ContainsKey((a.OwnerType, a.OwnerId))).Select(a => a.ToDto()).ToList();
    }
}
