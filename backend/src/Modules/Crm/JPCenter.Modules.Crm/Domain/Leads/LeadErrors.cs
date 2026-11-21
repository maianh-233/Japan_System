using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Leads;

public static class LeadErrors
{
    public static readonly Error NotFound = Error.NotFound("crm.lead.not_found", "Không tìm thấy lead");
    public static readonly Error NameRequired = Error.Validation("crm.lead.name_required", "Chưa nhập họ tên");
    public static readonly Error PhoneRequired = Error.Validation("crm.lead.phone_required", "Chưa nhập số điện thoại");
    public static readonly Error InvalidPhone = Error.Validation("crm.lead.invalid_phone", "SĐT gồm 10 số, bắt đầu bằng 0");
    public static readonly Error InvalidEmail = Error.Validation("crm.lead.invalid_email", "Email không hợp lệ");
    public static readonly Error ConsultContentRequired = Error.Validation("crm.lead.consult_content_required", "Chưa nhập nội dung tư vấn");
    public static readonly Error LostReasonRequired = Error.Validation("crm.lead.lost_reason_required", "Cần ghi lý do mất lead");
    public static readonly Error WonRequiresEnrollment =
        Error.Conflict("crm.lead.won_requires_enrollment", "Chỉ chuyển \"Đã chốt\" bằng thao tác Ghi danh");
    public static readonly Error WonLocked = Error.Conflict("crm.lead.won_locked", "Lead đã chốt (đã có ghi danh) — không đổi giai đoạn được");
    public static readonly Error AlreadyLinked = Error.Conflict("crm.lead.already_linked", "Học viên đã được liên kết với lead này");
    public static readonly Error SelfAlreadyLinked =
        Error.Conflict("crm.lead.self_already_linked", "Lead đã có hồ sơ học viên của chính mình (quan hệ \"Bản thân\")");
    public static readonly Error NotLinked = Error.NotFound("crm.lead.not_linked", "Học viên chưa liên kết với lead này");
    public static readonly Error AlreadyHidden = Error.Conflict("crm.lead.already_hidden", "Lead đã bị ẩn");
    public static readonly Error SlaAlreadyReassigned = Error.Conflict("crm.lead.sla_already_reassigned", "Lead đã được tự chuyển Sale 1 lần");
    public static readonly Error InvalidAssignee =
        Error.Validation("crm.lead.invalid_assignee", "Sale phụ trách phải còn làm việc và thuộc chi nhánh của lead");

    /// <summary>CRM-02.E3 — CẢNH BÁO (không chặn): trùng SĐT với lead khác</summary>
    public static readonly Error DuplicatePhone = Error.Conflict("crm.lead.duplicate_phone", "Số điện thoại trùng với lead khác");

    /// <summary>CRM-03.E4 — file nhập quá 1.000 dòng</summary>
    public static readonly Error ImportTooManyRows = Error.Validation("crm.import.too_many_rows", "File quá 1.000 dòng — chia nhỏ file rồi nhập lại");
}
