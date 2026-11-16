using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Messages;

/// <summary>Id mẫu tin nhắn</summary>
public readonly record struct MessageTemplateId(Guid Value)
{
    public static MessageTemplateId New() => new(IdGenerator.NewId());
}
