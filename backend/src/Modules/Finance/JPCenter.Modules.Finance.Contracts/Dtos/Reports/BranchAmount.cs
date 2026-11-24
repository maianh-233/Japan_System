namespace JPCenter.Modules.Finance.Contracts.Dtos.Reports;

/// <summary>1 số tiền theo chi nhánh (VND)</summary>
public sealed record BranchAmount(Guid BranchId, decimal Amount);
