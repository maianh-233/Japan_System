using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Exams.DrawExamQuestions;

/// <summary>
/// ND-04 bước 3 — xem trước đề: bốc câu đã duyệt trong phạm vi bài của mốc cho các phần (phần có sẵn câu giữ nguyên; đổi 1 câu = gửi
/// phần đó với <c>Count</c> = 1 và <c>Exclude</c> các câu đang có). Chưa lưu gì — tạo / sửa kỳ thi mới lưu.
/// </summary>
public sealed record DrawExamQuestionsCommand(Guid ClassId, Guid ExamId, IReadOnlyList<ExamSectionInput> Sections, IReadOnlyList<Guid>? Exclude)
    : ICommand<IReadOnlyList<ExamPaperSectionDto>>;

public sealed class DrawExamQuestionsValidator : AbstractValidator<DrawExamQuestionsCommand>
{
    public DrawExamQuestionsValidator() => RuleFor(x => x.Sections).NotEmpty().WithMessage("Thêm ít nhất 1 phần thi");
}

public sealed class DrawExamQuestionsHandler(LearningAccess access, ExamScope scope, ExamDrafter drafter)
    : ICommandHandler<DrawExamQuestionsCommand, IReadOnlyList<ExamPaperSectionDto>>
{
    public async Task<Result<IReadOnlyList<ExamPaperSectionDto>>> Handle(DrawExamQuestionsCommand cmd, CancellationToken ct)
    {
        var cls = await access.ClassAsync(cmd.ClassId, false, PermissionAction.Update, ct);
        if (cls.IsFailure) return cls.Error;
        var cur = await scope.OfAsync(cls.Value, ct);
        var milestone = new ExamMilestoneId(cmd.ExamId);
        if (cur.Find(milestone) is null) return ExamScope.MilestoneNotFound;

        var built = await drafter.BuildAsync(cur.Level, cur.Scopes[milestone], cmd.Sections, cmd.Exclude, ct);
        if (built.IsFailure) return built.Error;
        return Result.Success(await drafter.PaperAsync(built.Value, ct));
    }
}
