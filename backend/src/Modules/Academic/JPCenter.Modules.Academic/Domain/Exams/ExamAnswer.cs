using JPCenter.Modules.Academic.Domain.Questions;

namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>Câu trả lời đã lưu (jsonb): chỉ số đáp án GỐC của câu hỏi (đã đổi từ vị trí hiển thị)</summary>
public sealed record ExamAnswer(QuestionId QuestionId, int Choice);
