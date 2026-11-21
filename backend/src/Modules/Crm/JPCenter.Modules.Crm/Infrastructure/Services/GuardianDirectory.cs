using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="IGuardianDirectory"/> — đọc <c>crm.lead_student_links</c>. Lead đã ẩn vẫn tính (ẩn lead không ảnh hưởng học viên liên kết).
/// Bỏ qua bộ lọc chi nhánh / xóa mềm vì cổng PH không có phạm vi chi nhánh.
/// </summary>
internal sealed class GuardianDirectory(CrmDbContext db) : IGuardianDirectory
{
    public async Task<IReadOnlyList<Guid>> ChildrenOfAsync(Guid guardianLeadId, CancellationToken ct) =>
        await db.Leads.AsNoTracking().IgnoreQueryFilters()
            .Where(l => l.Id == new LeadId(guardianLeadId))
            .SelectMany(l => l.StudentLinks.Select(k => k.StudentId))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, GuardianContact>> DecidersOfAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken ct)
    {
        var ids = studentIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, GuardianContact>();
        var rows = await db.Leads.AsNoTracking().IgnoreQueryFilters()
            .SelectMany(l => l.StudentLinks.Where(k => k.IsPrimary && ids.Contains(k.StudentId))
                .Select(k => new { k.StudentId, LeadId = l.Id, l.FullName, l.Phone, k.Relationship }))
            .ToListAsync(ct);
        return rows.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g =>
        {
            var r = g.First();
            return new GuardianContact(r.LeadId.Value, r.FullName, r.Phone, SnakeCaseEnumConverter<Relationship>.ToText(r.Relationship));
        });
    }

    public async Task<IReadOnlyList<GuardianProfile>> ProfilesAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken ct)
    {
        var ids = leadIds.Distinct().Select(id => new LeadId(id)).ToList();
        if (ids.Count == 0) return [];
        var rows = await db.Leads.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.FullName, l.Phone, l.Email, l.BranchId, Children = l.StudentLinks.Select(k => k.StudentId).ToList() })
            .ToListAsync(ct);
        return rows.Select(r => new GuardianProfile(r.Id.Value, r.FullName, r.Phone, r.Email, r.BranchId.Value, r.Children)).ToList();
    }
}
