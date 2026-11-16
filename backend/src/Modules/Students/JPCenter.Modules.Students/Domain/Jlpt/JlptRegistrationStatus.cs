namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>Tiến độ đăng ký thi JLPT</summary>
public enum JlptRegistrationStatus
{
    /// <summary>Đã đăng ký</summary>
    Registered,
    /// <summary>Đã đóng lệ phí</summary>
    Paid,
    /// <summary>Đã nộp hội đồng thi</summary>
    Submitted,
    /// <summary>Có phiếu dự thi</summary>
    Admit,
    /// <summary>Đã có kết quả</summary>
    Done,
    /// <summary>Vắng thi</summary>
    Absent,
    /// <summary>Hủy đăng ký</summary>
    Cancelled,
}
