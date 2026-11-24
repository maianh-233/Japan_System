using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Crm.Contracts.Dtos;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Infrastructure.Services;

/// <summary>Hiện thực <see cref="ILeadOwnership"/> — HV liên kết với lead do Sale phụ trách (kể cả lead đã ẩn).</summary>
internal sealed class LeadOwnership(CrmDbContext db) : ILeadOwnership
{
    public async Task<IReadOnlyList<Guid>> StudentIdsOfSaleAsync(Guid employeeId, CancellationToken ct) =>
        await db.Leads.AsNoTracking().IgnoreQueryFilters()
            .Where(l => l.AssigneeId == employeeId)
            .SelectMany(l => l.StudentLinks.Select(k => k.StudentId))
            .Distinct()
            .ToListAsync(ct);

    public async Task<StudentLeadSales> SalesOfStudentAsync(Guid studentId, CancellationToken ct)
    {
        var leads = await db.Leads.AsNoTracking().IgnoreQueryFilters()
            .Where(l => !EF.Property<bool>(l, ShadowColumns.IsDeleted) && l.AssigneeId != null && l.StudentLinks.Any(k => k.StudentId == studentId))
            .Select(l => new { l.AssigneeId, Won = l.Status == LeadStatus.Won, CreatedAt = EF.Property<DateTimeOffset>(l, ShadowColumns.CreatedAt) })
            .ToListAsync(ct);
        if (leads.Count == 0) return StudentLeadSales.None;
        var newest = leads.OrderByDescending(l => l.CreatedAt).ToList();
        return new StudentLeadSales(newest.FirstOrDefault(l => l.Won)?.AssigneeId, newest[0].AssigneeId);
    }
}
