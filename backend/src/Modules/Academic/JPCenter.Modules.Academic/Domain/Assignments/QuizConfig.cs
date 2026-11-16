using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Assignments;

/// <summary>Tiêu chí bốc câu từ ngân hàng cho bài trắc nghiệm</summary>
/// <param name="TimeLimitMinutes">null = không giới hạn</param>
/// <param name="Attempts">Số lần được làm (lấy điểm cao nhất)</param>
/// <param name="Shuffle">Mỗi HV 1 đề khác (đảo câu + đáp án)</param>
public sealed record QuizConfig(
    List<LessonId> LessonIds,
    List<JapaneseSkill> Skills,
    QuizDifficulty Difficulty,
    int QuestionCount,
    int? TimeLimitMinutes,
    int Attempts,
    bool Shuffle);
