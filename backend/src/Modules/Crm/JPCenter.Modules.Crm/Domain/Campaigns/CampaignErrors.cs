using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Campaigns;

public static class CampaignErrors
{
    public static readonly Error NotFound = Error.NotFound("crm.campaign.not_found", "Không tìm thấy chiến dịch");
    public static readonly Error NameRequired = Error.Validation("crm.campaign.name_required", "Chưa nhập tên chiến dịch");
    public static readonly Error CodeRequired = Error.Validation("crm.campaign.code_required", "Chưa nhập mã chiến dịch");
    public static readonly Error BranchRequired = Error.Validation("crm.campaign.branch_required", "Chọn ít nhất 1 chi nhánh");
    public static readonly Error InvalidDates = Error.Validation("crm.campaign.invalid_dates", "Ngày kết thúc phải sau ngày bắt đầu");
    public static readonly Error InvalidTarget = Error.Validation("crm.campaign.invalid_target", "Chỉ tiêu lead phải lớn hơn 0");
    public static readonly Error DuplicateCode = Error.Conflict("crm.campaign.duplicate_code", "Mã chiến dịch đã tồn tại");
    public static readonly Error DuplicateUtm = Error.Conflict("crm.campaign.duplicate_utm", "Mã UTM đã dùng cho chiến dịch khác");
    public static readonly Error InvalidStatus = Error.Conflict("crm.campaign.invalid_status", "Không chuyển được trạng thái chiến dịch");
}
