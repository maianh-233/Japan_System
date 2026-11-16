using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.CareCases;

/// <summary>Id trường hợp cảnh báo bỏ học</summary>
public readonly record struct CareCaseId(Guid Value)
{
    public static CareCaseId New() => new(IdGenerator.NewId());
}
