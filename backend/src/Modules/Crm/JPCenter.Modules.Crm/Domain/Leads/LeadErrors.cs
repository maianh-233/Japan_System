using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Leads;

public static class LeadErrors
{
    public static readonly Error NameRequired = Error.Validation("crm.lead.name_required", "Chưa nhập họ tên");
    public static readonly Error PhoneRequired = Error.Validation("crm.lead.phone_required", "Chưa nhập số điện thoại");
    public static readonly Error ConsultContentRequired = Error.Validation("crm.lead.consult_content_required", "Chưa nhập nội dung tư vấn");
    public static readonly Error LostReasonRequired = Error.Validation("crm.lead.lost_reason_required", "Cần ghi lý do mất lead");
    public static readonly Error AlreadyLinked = Error.Conflict("crm.lead.already_linked", "Học viên đã được liên kết với lead này");
    public static readonly Error AlreadyHidden = Error.Conflict("crm.lead.already_hidden", "Lead đã bị ẩn");
}
