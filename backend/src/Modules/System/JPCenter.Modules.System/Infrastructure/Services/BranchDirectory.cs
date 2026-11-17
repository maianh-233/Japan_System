using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IBranchDirectory"/> cho module khác.</summary>
internal sealed class BranchDirectory(SystemDbContext db) : IBranchDirectory
{
    public async Task<BranchSummary?> FindAsync(Guid branchId, CancellationToken ct)
    {
        var b = await db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new BranchId(branchId), ct);
        return b is null ? null : ToSummary(b);
    }

    public async Task<IReadOnlyList<BranchSummary>> ListAsync(bool activeOnly, CancellationToken ct)
    {
        var query = db.Branches.AsNoTracking();
        if (activeOnly) query = query.Where(x => x.Status == BranchStatus.Active);
        return (await query.OrderBy(x => x.Code).ToListAsync(ct)).Select(ToSummary).ToList();
    }

    private static BranchSummary ToSummary(Branch b) => new(b.Id.Value, b.Code, b.Name, BranchReader.StatusText(b.Status), b.Address, b.Phone, b.Email,
        b.InvoicePrefix, b.BankName, b.BankAccount, b.BankHolder);
}
