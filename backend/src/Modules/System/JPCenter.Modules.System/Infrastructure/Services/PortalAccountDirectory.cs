using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IPortalAccountDirectory"/>: tài khoản cổng HV / PH đang hoạt động theo chủ tài khoản.</summary>
internal sealed class PortalAccountDirectory(SystemDbContext db) : IPortalAccountDirectory
{
    public async Task<IReadOnlyList<Guid>> ActiveAccountsAsync(IReadOnlyCollection<Guid> studentIds, IReadOnlyCollection<Guid> guardianLeadIds,
        CancellationToken ct)
    {
        if (studentIds.Count + guardianLeadIds.Count == 0) return [];
        var students = studentIds.Distinct().ToList();
        var guardians = guardianLeadIds.Distinct().ToList();
        var ids = await db.PortalAccounts.AsNoTracking()
            .Where(a => a.Status == PortalAccountStatus.Active
                        && ((a.OwnerType == PortalOwnerType.Student && students.Contains(a.OwnerId))
                            || (a.OwnerType == PortalOwnerType.Guardian && guardians.Contains(a.OwnerId))))
            .Select(a => a.Id).ToListAsync(ct);
        return ids.Select(i => i.Value).Distinct().ToList();
    }
}
