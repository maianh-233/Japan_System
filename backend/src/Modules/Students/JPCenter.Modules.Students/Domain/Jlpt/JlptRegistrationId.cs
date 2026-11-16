using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>Id lượt đăng ký thi JLPT</summary>
public readonly record struct JlptRegistrationId(Guid Value)
{
    public static JlptRegistrationId New() => new(IdGenerator.NewId());
}
