using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Employees;

/// <summary>Hợp đồng đã thay thế (gia hạn / ký mới — NS-06.E5). Bảng: hr.employment_contracts</summary>
public sealed class ContractHistoryEntry
{
    private ContractHistoryEntry() { }

    internal ContractHistoryEntry(EmploymentContract contract, DateTimeOffset replacedAt, Guid? replacedBy)
    {
        Id = IdGenerator.NewId();
        (Type, No, Start, End, BaseSalary, Allowance) = (contract.Type, contract.No, contract.Start, contract.End, contract.BaseSalary, contract.Allowance);
        (ReplacedAt, ReplacedBy) = (replacedAt, replacedBy);
    }

    public Guid Id { get; private set; }
    public ContractType Type { get; private set; }
    public string No { get; private set; } = null!;
    public DateOnly Start { get; private set; }
    public DateOnly? End { get; private set; }
    public Money? BaseSalary { get; private set; }
    public Money? Allowance { get; private set; }
    /// <summary>Lúc bị thay bằng hợp đồng mới</summary>
    public DateTimeOffset ReplacedAt { get; private set; }
    /// <summary>Tài khoản thực hiện (System.UserAccount)</summary>
    public Guid? ReplacedBy { get; private set; }
}
