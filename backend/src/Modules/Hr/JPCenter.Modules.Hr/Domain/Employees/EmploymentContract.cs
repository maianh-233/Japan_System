using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Employees;

/// <summary>Hợp đồng hiện tại (owned trong employees, cột contract_*). GV: lương cơ bản trống, tính theo giờ dạy.</summary>
public sealed record EmploymentContract(ContractType Type, string No, DateOnly Start, DateOnly? End, Money? BaseSalary, Money? Allowance);
