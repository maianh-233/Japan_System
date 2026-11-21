using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Common;

/// <summary>
/// Phạm vi quyền Tuyển sinh (PQ-10): <c>all</c> mọi chi nhánh · <c>branch</c> chi nhánh được gán · <c>own</c> chi nhánh được gán VÀ
/// lead có <c>assignee_id</c> = mình (quy tắc vàng 5). Ngoài phạm vi → coi như không tồn tại (404).
/// </summary>
public sealed class CrmAccess(ICurrentUser user)
{
    public const string Leads = "crm.leads";
    public const string LeadRules = "crm.lead-rules";
    public const string PlacementTests = "crm.placement-tests";
    public const string Appointments = "crm.appointments";
    public const string Campaigns = "crm.campaigns";
    public const string MessageTemplates = "crm.message-templates";
    public const string Funnel = "crm.funnel";

    public static readonly Error BranchNotAllowed = Error.Forbidden("crm.branch_not_allowed",
        "Bạn chỉ thao tác được dữ liệu của chi nhánh mình được phân công");

    /// <summary>Người thao tác (Hr.Employee) — tài khoản không gắn nhân viên dùng Id tài khoản</summary>
    public Guid ActorId => user.Access?.EmployeeId ?? user.AccountId ?? Guid.Empty;

    public string ActorName => user.Access?.DisplayName ?? "Hệ thống";

    public Guid? AccountId => user.AccountId;

    public bool Can(string page, PermissionAction action) => user.Can(page, action);

    /// <summary>Chi nhánh được thao tác; null = mọi chi nhánh; rỗng = không có quyền</summary>
    public HashSet<Guid>? Branches(string page, PermissionAction action) => user.AllowedBranches(page, action);

    public bool InBranch(string page, PermissionAction action, Guid branchId) =>
        Branches(page, action) is not { } allowed || allowed.Contains(branchId);

    /// <summary>Phạm vi own: Id nhân viên của mình để lọc; null = không giới hạn theo người</summary>
    public Guid? OwnOnly(string page, PermissionAction action) =>
        user.Permission(page, action) is { Scope: PermissionScope.Own } ? user.Access?.EmployeeId ?? Guid.Empty : null;

    /// <summary>Lọc lead theo phạm vi của hành động trên trang Lead</summary>
    public IQueryable<Lead> Scope(IQueryable<Lead> leads, PermissionAction action, string page = Leads)
    {
        if (Branches(page, action) is { } allowed)
        {
            var ids = allowed.Select(b => new BranchId(b)).ToList();
            leads = leads.Where(l => ids.Contains(l.BranchId));
        }
        if (OwnOnly(page, action) is { } me) leads = leads.Where(l => l.AssigneeId == me);
        return leads;
    }

    /// <summary>Lead trong phạm vi của hành động</summary>
    public bool Covers(Lead lead, PermissionAction action, string page = Leads) =>
        InBranch(page, action, lead.BranchId.Value) && (OwnOnly(page, action) is not { } me || lead.AssigneeId == me);
}
