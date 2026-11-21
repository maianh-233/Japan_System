using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Campaigns;

/// <summary>Nội dung chiến dịch (CRM-16 tạo / sửa) — kiểm tra trong <see cref="Campaign"/>. Trùng mã / UTM kiểm ở Application.</summary>
/// <param name="OwnerId">Người phụ trách (Hr.Employee)</param>
/// <param name="CourseIds">Khóa quảng bá (Academic.Course)</param>
public sealed record CampaignDetails(
    string Code,
    string Name,
    LeadSource Channel,
    IReadOnlyList<BranchId> BranchIds,
    DateOnly StartDate,
    DateOnly EndDate,
    Money Budget,
    Money Spent,
    int? TargetLeads,
    Guid OwnerId,
    string? Utm,
    IReadOnlyList<Guid> CourseIds,
    string? Note);
