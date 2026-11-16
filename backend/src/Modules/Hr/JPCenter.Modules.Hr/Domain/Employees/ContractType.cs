namespace JPCenter.Modules.Hr.Domain.Employees;

/// <summary>Loại hợp đồng</summary>
public enum ContractType
{
    /// <summary>Thử việc</summary>
    Probation,
    /// <summary>Có thời hạn</summary>
    Fixed,
    /// <summary>Không thời hạn</summary>
    Indefinite,
    /// <summary>Bán thời gian</summary>
    PartTime,
    /// <summary>Cộng tác viên</summary>
    Freelance,
}
