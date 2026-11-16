namespace JPCenter.Modules.Academic.Domain.Questions;

/// <summary>Trạng thái câu hỏi</summary>
public enum QuestionStatus
{
    /// <summary>Nháp</summary>
    Draft,
    /// <summary>Chờ duyệt</summary>
    Review,
    /// <summary>Đã duyệt (được bốc vào đề)</summary>
    Approved,
    /// <summary>Ngừng dùng</summary>
    Retired,
}
