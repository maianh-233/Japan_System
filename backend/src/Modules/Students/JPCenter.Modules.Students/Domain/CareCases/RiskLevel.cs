namespace JPCenter.Modules.Students.Domain.CareCases;

/// <summary>Mức nguy cơ bỏ học</summary>
public enum RiskLevel
{
    /// <summary>Thấp — theo dõi thêm</summary>
    Low,
    /// <summary>Trung bình — liên hệ trong 2–3 ngày</summary>
    Medium,
    /// <summary>Cao — liên hệ trong ngày</summary>
    High,
}
