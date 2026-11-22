using JPCenter.Modules.Academic.Domain.Questions;

namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>1 câu trong đề riêng của HV (jsonb): phần thi, thứ tự đáp án hiển thị (vị trí → chỉ số đáp án gốc)</summary>
public sealed record AttemptQuestion(QuestionId QuestionId, int Section, List<int> OptionOrder);
