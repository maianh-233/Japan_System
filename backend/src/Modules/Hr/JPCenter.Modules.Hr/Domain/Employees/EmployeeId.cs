using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Employees;

/// <summary>Id nhân viên / giáo viên / trợ giảng</summary>
public readonly record struct EmployeeId(Guid Value)
{
    public static EmployeeId New() => new(IdGenerator.NewId());
}
