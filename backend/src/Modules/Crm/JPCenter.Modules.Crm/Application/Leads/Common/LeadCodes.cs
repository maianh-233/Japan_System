using System.Globalization;
using JPCenter.Modules.Crm.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Leads.Common;

/// <summary>Mã lead tiếp theo (LD0001…) = số lớn nhất đã dùng + 1. 1 instance nên không cần sequence; trùng mã → UQ chặn, người sau thử lại.</summary>
public sealed class LeadCodes(ICrmDbContext db)
{
    public async Task<int> NextNumberAsync(CancellationToken ct)
    {
        var codes = await db.Leads.IgnoreQueryFilters().Select(l => l.Code).ToListAsync(ct);
        return codes.Select(c => c.StartsWith("LD", StringComparison.Ordinal)
                && int.TryParse(c[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0).Max() + 1;
    }
}
