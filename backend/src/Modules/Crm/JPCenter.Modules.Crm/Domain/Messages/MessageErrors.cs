using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Messages;

public static class MessageErrors
{
    public static readonly Error TemplateNotFound = Error.NotFound("crm.message_template.not_found", "Không tìm thấy mẫu tin");
    public static readonly Error NameRequired = Error.Validation("crm.message_template.name_required", "Chưa nhập tên mẫu tin");
    public static readonly Error BodyRequired = Error.Validation("crm.message_template.body_required", "Chưa nhập nội dung mẫu tin");
    public static readonly Error MessageBodyRequired = Error.Validation("crm.message.body_required", "Chưa nhập nội dung tin");
    public static readonly Error RecipientRequired = Error.Validation("crm.message.recipient_required", "Chưa có người nhận (SĐT hoặc email)");
}
