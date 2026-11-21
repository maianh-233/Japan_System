using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Notifications.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Crm.Application.Common;

/// <summary>
/// Thông báo chuông của Tuyển sinh (TB-04) — phát <see cref="NotificationRequested"/> qua outbox, gọi TRƯỚC <c>SaveChangesAsync</c>
/// để đi cùng giao dịch. Người thao tác không tự nhận.
/// </summary>
public sealed class CrmNotices(IIntegrationEventPublisher events, ICurrentUser user)
{
    public static string LeadLink(Guid leadId) => $"/admin/crm/leads?open={leadId}";
    public const string RulesLink = "/admin/crm/lead-rules";
    public const string AppointmentsLink = "/admin/crm/appointments";

    /// <summary>CRM-02 bước 3, CRM-04 — Sale được giao lead</summary>
    public void LeadAssigned(Guid leadId, string leadName, string code, Guid saleId, string why) =>
        Publish(new NotificationRequested($"Lead mới được giao: {leadName}", $"{code} · {why}", "info", LeadLink(leadId), "crm.lead.assigned",
            EmployeeIds: [saleId]));

    /// <summary>CRM-10.E1 / E2 — chưa chia được → người quản lý quy tắc chia lead của chi nhánh</summary>
    public void LeadUnassigned(Guid leadId, string leadName, Guid branchId, string reason) =>
        Publish(new NotificationRequested($"Lead chưa có Sale: {leadName}", reason, "warning", LeadLink(leadId), "crm.lead.unassigned",
            Permission: $"{CrmAccess.LeadRules}.update", PermissionBranchId: branchId));

    /// <summary>CRM-11 — lead quá SLA (gom: &gt; 3 lead → 1 thông báo tổng hợp do job gọi)</summary>
    public void SlaBreached(string title, string description, string link, IReadOnlyList<Guid> escalateTo, Guid? branchId) =>
        Publish(escalateTo.Count > 0
            ? new NotificationRequested(title, description, "danger", link, "crm.lead.sla_breached", EmployeeIds: escalateTo)
            : new NotificationRequested(title, description, "danger", link, "crm.lead.sla_breached",
                Permission: $"{CrmAccess.LeadRules}.update", PermissionBranchId: branchId));

    /// <summary>CRM-11 bước 2 — đã tự chuyển Sale</summary>
    public void SlaReassigned(string title, string description, string link, IReadOnlyList<Guid> employees, Guid? branchId) =>
        Publish(new NotificationRequested(title, description, "warning", link, "crm.lead.sla_reassigned", EmployeeIds: employees,
            Permission: $"{CrmAccess.LeadRules}.update", PermissionBranchId: branchId));

    /// <summary>CRM-14 bước 4 — nhắc người tiếp trước giờ hẹn</summary>
    public void AppointmentSoon(Guid staffId, string title, string description) =>
        Publish(new NotificationRequested(title, description, "info", AppointmentsLink, "crm.appointment.reminder", EmployeeIds: [staffId]));

    /// <summary>Sale nghỉ việc còn giữ lead mở → người quản lý chia lead (nghe <c>EmployeeQuit</c>)</summary>
    public void SaleQuit(string name, int openLeads, IReadOnlyCollection<Guid> branchIds) =>
        Publish(new NotificationRequested($"{name} đã nghỉ việc — còn {openLeads} lead đang mở",
            "Chuyển các lead này cho Sale khác ở trang Lead (lọc theo Sale).", "warning", "/admin/crm/leads", "crm.sale.quit",
            Permission: $"{CrmAccess.LeadRules}.update", PermissionBranchId: branchIds.Count == 1 ? branchIds.First() : null));

    private void Publish(NotificationRequested n) => events.Publish(n with { ExcludeAccountId = user.AccountId });
}
