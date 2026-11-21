using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Students.Contracts.Dtos;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Guardians.Common;

/// <summary>
/// Phạm vi trang Học viên › Phụ huynh (HV-12, quyền <c>students.contacts</c> — giáo vụ không cần quyền Tuyển sinh):
/// phụ huynh thuộc chi nhánh được gán HOẶC có con học ở chi nhánh được gán; phạm vi own = lead do mình phụ trách.
/// </summary>
public sealed class GuardianAccess(ICrmDbContext db, CrmAccess access, IEnrollmentLookup students)
{
    public const string Page = "students.contacts";

    public static readonly Error StudentNotAllowed =
        Error.Forbidden("crm.branch_not_allowed", "Học viên thuộc chi nhánh bạn không được phân công");

    public bool Can(PermissionAction action) => access.Can(Page, action);

    public bool InBranch(PermissionAction action, Guid branchId) => access.InBranch(Page, action, branchId);

    /// <summary>Lead có ít nhất 1 liên kết HV quan hệ khác "Bản thân", trong phạm vi</summary>
    public async Task<List<Lead>> ListAsync(PermissionAction action, CancellationToken ct)
    {
        var q = db.Leads.AsNoTracking().IgnoreQueryFilters().Where(l => l.StudentLinks.Any(k => k.Relationship != Relationship.Self));
        if (access.OwnOnly(Page, action) is { } me) q = q.Where(l => l.AssigneeId == me);
        return await FilterAsync(await q.OrderBy(l => l.FullName).ToListAsync(ct), action, ct);
    }

    /// <summary>Nạp 1 người liên hệ để sửa / liên kết (kể cả lead đã ẩn); ngoài phạm vi → null (404)</summary>
    public async Task<Lead?> LoadAsync(Guid id, PermissionAction action, CancellationToken ct, uint? version = null)
    {
        var lead = await db.Leads.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == new LeadId(id), ct);
        if (lead is null) return null;
        if (access.OwnOnly(Page, action) is { } me && lead.AssigneeId != me) return null;
        if ((await FilterAsync([lead], action, ct)).Count == 0) return null;
        if (version is { } v && v != 0) ((DbContext)db).Entry(lead).Property("xmin").OriginalValue = v;
        return lead;
    }

    /// <summary>HV được thao tác: thuộc chi nhánh trong phạm vi của hành động</summary>
    public bool Covers(StudentSummary student, PermissionAction action) => InBranch(action, student.BranchId);

    private async Task<List<Lead>> FilterAsync(List<Lead> list, PermissionAction action, CancellationToken ct)
    {
        if (access.Branches(Page, action) is not { } allowed) return list;
        var ids = list.SelectMany(l => l.StudentLinks.Select(k => k.StudentId)).Distinct().ToList();
        var branchOf = ids.Count == 0 ? [] : (await students.SummariesAsync(ids, ct)).ToDictionary(s => s.Id, s => s.BranchId);
        return list.Where(l => allowed.Contains(l.BranchId.Value)
            || l.StudentLinks.Any(k => branchOf.TryGetValue(k.StudentId, out var b) && allowed.Contains(b))).ToList();
    }
}
