namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>Giai đoạn ứng viên</summary>
public enum CandidateStage
{
    /// <summary>Ứng tuyển</summary>
    Applied,
    /// <summary>Sàng lọc</summary>
    Screening,
    /// <summary>Phỏng vấn</summary>
    Interview,
    /// <summary>Đã gửi offer</summary>
    Offer,
    /// <summary>Nhận việc</summary>
    Hired,
    /// <summary>Loại</summary>
    Rejected,
}
