namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>Kết quả 1 lần tư vấn</summary>
public enum ConsultResult
{
    /// <summary>Quan tâm</summary>
    Interested,
    /// <summary>Đang suy nghĩ</summary>
    Thinking,
    /// <summary>Đã hẹn test</summary>
    TestBooked,
    /// <summary>Không nghe máy</summary>
    NoAnswer,
    /// <summary>Không quan tâm</summary>
    NotInterested,
    /// <summary>Đã đăng ký học</summary>
    Enrolled,
}
