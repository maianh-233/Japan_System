using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Assignments.Common;

/// <summary>Tiêu chí bốc câu — khớp <c>QuizConfig</c> (frontend <c>features/academic/classes/types.ts</c>)</summary>
public sealed record QuizConfigDto(
    IReadOnlyList<Guid> LessonIds,
    IReadOnlyList<JapaneseSkill> Skills,
    QuizDifficulty Difficulty,
    int QuestionCount,
    int? TimeLimitMinutes,
    int Attempts,
    bool Shuffle);

/// <summary>Bài tập — khớp <c>Assignment</c> (frontend). <c>CreatedBy</c> = tên người giao.</summary>
public sealed record AssignmentDto(
    Guid Id,
    Guid ClassId,
    AssignmentType Type,
    string Title,
    Guid? LessonId,
    string? Description,
    DateTimeOffset AssignedAt,
    DateTimeOffset DueDate,
    decimal MaxScore,
    QuizConfigDto? Quiz,
    IReadOnlyList<SubmissionFileDto> Attachments,
    Guid CreatedByTeacherId,
    string CreatedBy);

/// <summary>Kết quả quiz lần cao nhất</summary>
public sealed record SubmissionQuizDto(int Correct, int Total, int Attempts, int DurationMinutes);

/// <summary>Bài nộp — khớp <c>Submission</c> (frontend). <c>GradedBy</c> = tên GV chấm (null = hệ thống tự chấm). <c>Late</c> = nộp sau hạn (ND-02.E2).</summary>
public sealed record SubmissionDto(
    Guid Id,
    Guid AssignmentId,
    Guid StudentId,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<SubmissionFileDto> Files,
    SubmissionQuizDto? Quiz,
    string? Content,
    decimal? Score,
    string? Comment,
    DateTimeOffset? GradedAt,
    string? GradedBy,
    bool Late);

/// <summary>Tab Bài tập của lớp: bài tập + mọi bài nộp (frontend giữ 2 mảng riêng)</summary>
public sealed record ClassAssignmentsDto(IReadOnlyList<AssignmentDto> Assignments, IReadOnlyList<SubmissionDto> Submissions);

/// <summary>Kết quả giao / sửa bài: <c>Warning</c> = ND-01.E4 ngân hàng chưa đủ câu đúng bài học (vẫn giao, bù câu cùng / thấp hơn trình độ)</summary>
public sealed record AssignmentSavedDto(AssignmentDto Assignment, string? Warning);

/// <summary>Cổng HV / PH: 1 bài tập của lớp + bài nộp của HV + số lần quiz còn lại</summary>
public sealed record MyAssignmentDto(AssignmentDto Assignment, string ClassCode, string ClassName, SubmissionDto? Submission, int? AttemptsLeft,
    bool CanSubmit);

/// <summary>Dữ liệu nhập khi giao / sửa bài (ND-01)</summary>
public sealed record AssignmentInput(
    AssignmentType Type,
    string? Title,
    Guid? LessonId,
    string? Description,
    DateTimeOffset DueDate,
    decimal? MaxScore,
    QuizConfigDto? Quiz,
    IReadOnlyList<SubmissionFileDto>? Attachments);
