using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Messages;

/// <summary>Mẫu tin Zalo / SMS theo tình huống, biến {ten}, {lop}, {so_tien}... (CRM-17). Bảng: crm.message_templates</summary>
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
        var t = new MessageTemplate { Id = MessageTemplateId.New(), Active = true };
        var set = t.Update(scenario, name, channel, body);
        return set.IsFailure ? set.Error : t;
    }

    public Result Update(MessageScenario scenario, string name, ContactChannel channel, string body)
    {
        if (string.IsNullOrWhiteSpace(name)) return MessageErrors.NameRequired;
        if (string.IsNullOrWhiteSpace(body)) return MessageErrors.BodyRequired;
        (Scenario, Name, Channel, Body) = (scenario, name.Trim(), channel, body.Trim());
        return Result.Success();
    }

    public void SetActive(bool active) => Active = active;

    /// <summary>Nội dung đã điền biến</summary>
    public string Render(IReadOnlyDictionary<string, string?> vars) => MessageTemplateText.Fill(Body, vars);
}
