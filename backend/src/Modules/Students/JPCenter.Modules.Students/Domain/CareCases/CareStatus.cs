namespace JPCenter.Modules.Students.Domain.CareCases;

/// <summary>Tiến độ chăm sóc</summary>
public enum CareStatus
{
    /// <summary>Chưa liên hệ</summary>
    New,
    /// <summary>Đang liên hệ</summary>
    Contacting,
    /// <summary>Đang theo dõi</summary>
    Monitoring,
    /// <summary>Đã ổn định</summary>
    Resolved,
}
