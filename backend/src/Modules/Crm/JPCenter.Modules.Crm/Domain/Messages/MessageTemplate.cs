using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Messages;

/// <summary>Mẫu tin Zalo / SMS, biến {ten}, {lop}, {so_tien}... Bảng: crm.message_templates</summary>
public sealed class MessageTemplate : AggregateRoot<MessageTemplateId>, IAuditable, ISoftDeletable
{
    private MessageTemplate() { }

    public MessageScenario Scenario { get; private set; }
    public string Name { get; private set; } = null!;
    /// <summary>Kênh gợi ý (vẫn đổi được khi gửi)</summary>
    public ContactChannel Channel { get; private set; }
    public string Body { get; private set; } = null!;
    public bool Active { get; private set; }

    public static Result<MessageTemplate> Create(MessageScenario scenario, string name, ContactChannel channel, string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return Error.Validation("crm.message_template.body_required", "Chưa nhập nội dung mẫu tin");
        return new MessageTemplate { Id = MessageTemplateId.New(), Scenario = scenario, Name = name, Channel = channel, Body = body, Active = true };
    }

    public void Update(string name, ContactChannel channel, string body) => (Name, Channel, Body) = (name, channel, body);
    public void SetActive(bool active) => Active = active;
}
