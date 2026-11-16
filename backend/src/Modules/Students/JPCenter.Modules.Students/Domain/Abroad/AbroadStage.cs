namespace JPCenter.Modules.Students.Domain.Abroad;

/// <summary>Giai đoạn hồ sơ</summary>
public enum AbroadStage
{
    /// <summary>Tư vấn</summary>
    Consult,
    /// <summary>Hồ sơ giấy tờ</summary>
    Documents,
    /// <summary>Phỏng vấn</summary>
    Interview,
    /// <summary>Chờ COE</summary>
    Coe,
    /// <summary>Xin visa</summary>
    Visa,
    /// <summary>Đã xuất cảnh</summary>
    Departed,
}
