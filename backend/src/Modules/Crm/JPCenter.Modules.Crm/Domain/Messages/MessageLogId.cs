using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Messages;

/// <summary>Id tin đã gửi</summary>
public readonly record struct MessageLogId(Guid Value)
{
    public static MessageLogId New() => new(IdGenerator.NewId());
}
