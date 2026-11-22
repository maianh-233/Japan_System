using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Assignments.Common;

/// <summary>Dữ liệu đã kiểm để giao / sửa bài</summary>
public sealed record PreparedAssignment(LessonId? LessonId, QuizConfig? Quiz, List<AttachedFile> Attachments, string? Warning);

/// <summary>
/// Kiểm dữ liệu giao / sửa bài tập (ND-01) ngoài bất biến của domain: lớp chưa đóng (E5), bài học thuộc lộ trình lớp,
/// tệp đính kèm hợp lệ, cảnh báo ngân hàng chưa đủ câu (E4).
/// </summary>
public sealed class AssignmentEditor(QuizDrawer drawer, LearningFiles files)
{
    public const decimal DefaultMaxScore = 10;

    public async Task<Result<PreparedAssignment>> PrepareAsync(Class cls, AssignmentInput input, IReadOnlyCollection<AttachedFile>? existing,
        CancellationToken ct)
    {
        if (cls.IsClosed) return ClassErrors.Closed;
        var (level, lessons) = await drawer.CurriculumOfAsync(cls, ct);
        LessonId? lessonId = input.LessonId is { } l ? new LessonId(l) : null;
        if (lessonId is { } lid && !lessons.Contains(lid)) return QuizDrawer.LessonInvalid;

        QuizConfig? quiz = null;
        string? warning = null;
        if (input.Type == AssignmentType.Quiz)
        {
            if (input.Quiz is not { } q) return AssignmentErrors.QuizConfig;
            var ids = q.LessonIds.Distinct().Select(x => new LessonId(x)).ToList();
            if (ids.Any(x => !lessons.Contains(x))) return QuizDrawer.LessonInvalid;
            quiz = new QuizConfig(ids, q.Skills.Distinct().ToList(), q.Difficulty, q.QuestionCount, q.TimeLimitMinutes, q.Attempts, q.Shuffle);
            if (ids.Count > 0 && quiz.Skills.Count > 0 && q.QuestionCount is > 0 and <= Assignment.MaxQuestions)
            {
                var exact = await drawer.CountExactAsync(level, quiz, ct);
                if (exact < q.QuestionCount)
                    warning = $"Chỉ có {exact} câu phù hợp — hệ thống bù câu cùng trình độ / thấp hơn 1 bậc";
            }
        }

        var attached = await files.ToAttachedAsync(input.Type == AssignmentType.Upload ? input.Attachments : [], ct, existing);
        if (attached.IsFailure) return attached.Error;
        return new PreparedAssignment(lessonId, quiz, attached.Value, warning);
    }
}
