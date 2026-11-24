using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Infrastructure.Services;

/// <summary>Hiện thực <see cref="ITuitionQuery"/> — quá hạn tính khi đọc (<c>Invoice.Summarize</c>, TC-11.E3), không có job đổi trạng thái.</summary>
public sealed class TuitionQuery(FinanceDbContext db, IClock clock) : ITuitionQuery
{
    public async Task<IReadOnlySet<Guid>> OverdueStudentsAsync(IReadOnlyCollection<Guid>? studentIds, CancellationToken ct)
    {
        var today = clock.Today;
        var query = db.Invoices.AsNoTracking().Where(i => i.Status == InvoiceStatus.Open);
        if (studentIds is not null) query = query.Where(i => studentIds.Contains(i.StudentId));
        var open = await query.ToListAsync(ct);
        return open.Where(i => i.Summarize(today).Status == TuitionStatus.Overdue).Select(i => i.StudentId).ToHashSet();
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> DebtByEnrollmentAsync(IReadOnlyCollection<Guid> enrollmentIds, CancellationToken ct)
    {
        if (enrollmentIds.Count == 0) return new Dictionary<Guid, decimal>();
        var open = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Open && enrollmentIds.Contains(i.EnrollmentId)).ToListAsync(ct);
        return open.GroupBy(i => i.EnrollmentId).ToDictionary(g => g.Key, g => g.Sum(i => i.Remaining.Amount));
    }
}
