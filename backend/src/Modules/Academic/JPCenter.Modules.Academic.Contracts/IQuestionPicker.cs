using JPCenter.Modules.Academic.Contracts.Dtos;

namespace JPCenter.Modules.Academic.Contracts;

/// <summary>
/// Bốc câu từ Ngân hàng câu hỏi cho đề (user story 11, NH-09 / NH-10) — quiz, kỳ thi (Academic), test xếp lớp (Crm).
/// Chỉ câu đã duyệt. Nội dung bốc ra KHÔNG có đáp án — đáp án lấy riêng khi chấm.
/// </summary>
public interface IQuestionPicker
{
    /// <summary>Bốc ngẫu nhiên theo tiêu chí, tăng "số đề đã dùng" của câu được bốc. Không đủ câu → trả số có được + <c>Missing</c>.</summary>
    Task<QuestionPick> PickAsync(QuestionPickCriteria criteria, CancellationToken ct);

    /// <summary>Nội dung (không đáp án) theo Id, đúng thứ tự — hiển thị lại đề đã bốc. Câu đã ngừng dùng vẫn trả (đề cũ giữ nguyên).</summary>
    Task<IReadOnlyList<PickedQuestion>> GetAsync(IReadOnlyList<Guid> questionIds, CancellationToken ct);

    /// <summary>Đáp án đúng để chấm: Id câu → vị trí đáp án đúng (0-based)</summary>
    Task<IReadOnlyDictionary<Guid, int>> AnswersAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken ct);

    /// <summary>NH-10 — ghi lượt làm sau khi chấm (picked = -1: bỏ trống)</summary>
    Task RecordAnswersAsync(IReadOnlyList<QuestionAnswer> answers, CancellationToken ct);
}
