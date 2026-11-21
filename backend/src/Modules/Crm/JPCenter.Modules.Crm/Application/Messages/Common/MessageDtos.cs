using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Messages.Common;

/// <summary>Mẫu tin — khớp <c>MessageTemplate</c> frontend</summary>
public sealed record MessageTemplateDto(Guid Id, MessageScenario Scenario, string Name, ContactChannel Channel, string Body, bool Active,
    DateTimeOffset UpdatedAt, uint Version);

/// <summary>Tin đã gửi — khớp <c>MessageLog</c> frontend (+ trạng thái gửi)</summary>
public sealed record MessageLogDto(Guid Id, ContactChannel Channel, MessageScenario Scenario, Guid? TemplateId, string ToName, string ToPhone,
    Guid? LeadId, Guid? StudentId, string Body, Guid? SentById, string SentBy, DateTimeOffset SentAt, bool Auto, DeliveryStatus Status);
