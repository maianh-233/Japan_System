using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>Id đăng ký ca / lịch rảnh</summary>
public readonly record struct ShiftRegistrationId(Guid Value)
{
    public static ShiftRegistrationId New() => new(IdGenerator.NewId());
}
