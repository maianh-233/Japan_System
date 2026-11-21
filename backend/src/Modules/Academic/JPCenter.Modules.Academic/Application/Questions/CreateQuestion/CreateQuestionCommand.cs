using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Questions.CreateQuestion;

/// <summary>NH-02 — thêm câu hỏi: lưu nháp, hoặc gửi duyệt luôn (<paramref name="Submit"/>). Người soạn = người đang đăng nhập.</summary>
public sealed record CreateQuestionCommand(QuestionInput Input, bool Submit) : ICommand<QuestionDto>;

public sealed class CreateQuestionHandler(IAcademicDbContext db, QuestionBank bank, IAuditTrail audit) : ICommandHandler<CreateQuestionCommand, QuestionDto>
{
    public async Task<Result<QuestionDto>> Handle(CreateQuestionCommand cmd, CancellationToken ct)
    {
        var content = cmd.Input.ToContent();
        if (await bank.CheckLessonsAsync(content, ct) is { IsFailure: true } lessons) return lessons.Error;

        var code = await bank.CodeGeneratorAsync(ct);
        var created = Question.Create(code(content.Level, content.Skill), content, bank.Me, cmd.Submit);
        if (created.IsFailure) return created.Error;

        db.Questions.Add(created.Value);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Thêm câu hỏi", created.Value.Code, cmd.Submit ? "Gửi duyệt" : "Lưu nháp");
        return await bank.ToDtoAsync(created.Value, ct);
    }
}
