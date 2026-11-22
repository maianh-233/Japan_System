using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Exams.Common;

/// <summary>1 phần thi. <c>QuestionIds</c> chỉ trả cho nhân viên / GV (cổng HV: rỗng)</summary>
public sealed record ExamSectionDto(string Name, JapaneseSkill? Skill, decimal Points, int QuestionCount, decimal Max, IReadOnlyList<Guid> QuestionIds);

/// <summary>Kỳ thi của lớp (ND-04). <c>ExamId</c> = mốc thi của lộ trình. <c>Attempts</c> = số bài làm online, <c>Results</c> = số kết quả đã có.</summary>
public sealed record ClassExamDto(
    Guid Id,
    Guid ClassId,
    Guid ExamId,
    ExamKind? Kind,
    string Title,
    ExamMode Mode,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int DurationMinutes,
    decimal PassPercent,
    bool Shuffle,
    IReadOnlyList<ExamSectionDto> Sections,
    decimal MaxScore,
    int QuestionCount,
    ClassExamStatus Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ResultsPublishedAt,
    int Attempts,
    int Results);

/// <summary>Mốc thi của lộ trình lớp + phạm vi bài (bài sau mốc trước → bài của mốc)</summary>
public sealed record ExamMilestoneDto(Guid Id, ExamKind Kind, string Title, Guid AfterLessonId, IReadOnlyList<Guid> LessonIds, Guid? ClassExamId);

/// <summary>Kết quả thi — khớp <c>ExamResult</c> (frontend). <c>ExamId</c> = mốc thi.</summary>
public sealed record ExamResultDto(
    Guid Id,
    Guid ClassId,
    Guid ExamId,
    Guid? ClassExamId,
    Guid StudentId,
    ExamAttendance Status,
    IReadOnlyList<ExamSectionScore> Sections,
    decimal TotalScore,
    decimal MaxScore,
    decimal? Percent,
    DateTimeOffset? StartedAt,
    int? DurationMinutes,
    string? Comment);

/// <summary>Tab Bài thi của lớp: mốc thi, kỳ thi, mọi kết quả</summary>
public sealed record ClassExamBoardDto(IReadOnlyList<ExamMilestoneDto> Milestones, IReadOnlyList<ClassExamDto> Exams, IReadOnlyList<ExamResultDto> Results);

/// <summary>1 câu trong đề cho nhân viên / GV (xem trước, bản in) — CÓ đáp án</summary>
public sealed record ExamQuestionDto(Guid Id, string Code, string Skill, string Difficulty, string Stem, string? Passage, IReadOnlyList<string> Options,
    int Answer, string? Explanation);

/// <summary>Phần thi kèm câu hỏi (xem trước khi tạo, bản in)</summary>
public sealed record ExamPaperSectionDto(string Name, JapaneseSkill? Skill, decimal Points, IReadOnlyList<ExamQuestionDto> Questions);

/// <summary>Phần thi nhập khi tạo / sửa kỳ thi: <c>QuestionIds</c> (đã xem trước / đổi câu) hoặc <c>Count</c> để server tự bốc</summary>
public sealed record ExamSectionInput(string? Name, JapaneseSkill? Skill, decimal Points, int? Count, IReadOnlyList<Guid>? QuestionIds);

/// <summary>Dữ liệu tạo / sửa kỳ thi (ND-04)</summary>
/// <param name="ExamId">Mốc thi của lộ trình lớp</param>
public sealed record ExamInput(
    Guid ExamId,
    string? Title,
    ExamMode Mode,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    decimal? PassPercent,
    bool Shuffle,
    IReadOnlyList<ExamSectionInput>? Sections);
