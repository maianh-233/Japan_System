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
}
