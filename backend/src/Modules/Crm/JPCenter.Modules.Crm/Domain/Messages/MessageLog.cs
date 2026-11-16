using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.Messages;

/// <summary>Nhật ký tin đã gửi cho lead / học viên / phụ huynh (chỉ thêm). Bảng: crm.message_logs</summary>
public sealed class MessageLog : Entity<MessageLogId>
{
    private MessageLog() { }

    public ContactChannel Channel { get; private set; }
    public MessageScenario Scenario { get; private set; }
    public MessageTemplateId? TemplateId { get; private set; }
    public string ToName { get; private set; } = null!;
    public string ToPhone { get; private set; } = null!;
    public LeadId? LeadId { get; private set; }
    /// <summary>Students.Student</summary>
    public Guid? StudentId { get; private set; }
    /// <summary>Nội dung thực tế đã gửi (đã điền biến)</summary>
    public string Body { get; private set; } = null!;
    /// <summary>Người gửi — null nếu hệ thống tự gửi</summary>
    public Guid? SentBy { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public bool Auto { get; private set; }

    public static MessageLog Create(ContactChannel channel, MessageScenario scenario, MessageTemplateId? templateId, string toName,
        string toPhone, LeadId? leadId, Guid? studentId, string body, Guid? sentBy, DateTimeOffset sentAt) =>
        new()
        {
            Id = MessageLogId.New(), Channel = channel, Scenario = scenario, TemplateId = templateId, ToName = toName, ToPhone = toPhone,
            LeadId = leadId, StudentId = studentId, Body = body, SentBy = sentBy, SentAt = sentAt, Auto = sentBy is null,
        };
}
