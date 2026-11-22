namespace JPCenter.Modules.Academic.Application.Abstractions;

/// <summary>1 câu trong đề quiz đã phát: thứ tự đáp án hiển thị (vị trí → chỉ số đáp án gốc)</summary>
public sealed record QuizTicketQuestion(Guid QuestionId, IReadOnlyList<int> OptionOrder);

/// <summary>
/// Đề quiz bài tập đã phát cho HV (ND-02) — gửi về cổng dưới dạng vé KÝ + MÃ HÓA (HV không đọc / sửa được), nộp bài gửi lại vé:
/// không cần lưu "lần làm dở" vào DB. <paramref name="AttemptNo"/> = số lần đã làm lúc phát đề → 1 vé chỉ nộp được 1 lần.
/// </summary>
public sealed record QuizTicket(Guid AssignmentId, Guid StudentId, int AttemptNo, DateTimeOffset StartedAt, IReadOnlyList<QuizTicketQuestion> Questions);

/// <summary>Ký / mở vé quiz (Infrastructure: ASP.NET Data Protection)</summary>
public interface IQuizTokens
{
    string Issue(QuizTicket ticket);

    /// <summary>Vé hỏng / bị sửa / quá 1 ngày → null</summary>
    QuizTicket? Read(string token);
}
