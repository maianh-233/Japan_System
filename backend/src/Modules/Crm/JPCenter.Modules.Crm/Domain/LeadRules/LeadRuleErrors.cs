using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.LeadRules;

public static class LeadRuleErrors
{
    /// <summary>SLA tối đa 7 ngày (phút)</summary>
    public const int MaxSlaMinutes = 7 * 24 * 60;

    public static readonly Error NotFound = Error.NotFound("crm.lead_rule.not_found", "Không tìm thấy quy tắc chia lead");
    public static readonly Error NameRequired = Error.Validation("crm.lead_rule.name_required", "Chưa nhập tên quy tắc");
    public static readonly Error NoSale = Error.Validation("crm.lead_rule.no_sale", "Quy tắc phải có ít nhất 1 Sale");
    public static readonly Error InvalidSla = Error.Validation("crm.lead_rule.invalid_sla", "SLA phải từ 1 phút đến 7 ngày");
    public static readonly Error InvalidOrder =
        Error.Validation("crm.lead_rule.invalid_order", "Danh sách thứ tự phải gồm đúng mỗi quy tắc 1 lần");
}
