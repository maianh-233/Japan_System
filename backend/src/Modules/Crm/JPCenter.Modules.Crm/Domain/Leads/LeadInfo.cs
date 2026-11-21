using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>Thông tin nhập tay của lead (CRM-02 thêm, CRM-04 sửa) — kiểm tra + chuẩn hóa trong <see cref="Lead"/>.</summary>
public sealed record LeadInfo(
    string FullName,
    string Phone,
    string? Email,
    LeadSource Source,
    BranchId BranchId,
    string? Interest,
    string? Note,
    CampaignId? CampaignId,
    DateTimeOffset? NextFollowUpAt);
