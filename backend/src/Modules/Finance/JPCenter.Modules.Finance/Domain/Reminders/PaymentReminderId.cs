using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Reminders;

/// <summary>Id lần nhắc học phí</summary>
public readonly record struct PaymentReminderId(Guid Value)
{
    public static PaymentReminderId New() => new(IdGenerator.NewId());
}
