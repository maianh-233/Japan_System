using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Questions.ImportQuestions;

/// <summary>
/// NH-08 — nhập nhiều câu trong 1 request (frontend đã đọc Excel + kiểm tra sơ bộ). Server kiểm lại từng dòng như NH-02, tạo dòng đúng,
/// trả lỗi theo số dòng. <paramref name="Submit"/> = gửi duyệt luôn; câu nghe hiểu (chưa có file nghe) luôn vào nháp.
/// </summary>
/// <param name="Rows">Mỗi dòng kèm số dòng trong file để báo lỗi</param>
public sealed record ImportQuestionsCommand(IReadOnlyList<ImportRow> Rows, bool Submit) : ICommand<ImportResult>;

public sealed record ImportRow(int Row, QuestionInput Input);

/// <param name="DraftListening">Số câu nghe hiểu đã tạo ở dạng nháp (cần tải file nghe rồi gửi duyệt)</param>
public sealed record ImportResult(IReadOnlyList<QuestionDto> Created, IReadOnlyList<ImportError> Errors, int DraftListening);

public sealed record ImportError(int Row, IReadOnlyList<string> Messages);

public sealed class ImportQuestionsValidator : AbstractValidator<ImportQuestionsCommand>
{
    public const int MaxRows = 500;

    public ImportQuestionsValidator() =>
        RuleFor(x => x.Rows).NotEmpty().WithMessage("Không có dòng nào để nhập")
            .Must(r => r.Count <= MaxRows).WithMessage($"Tối đa {MaxRows} câu mỗi lần nhập");
}

public sealed class ImportQuestionsHandler(IAcademicDbContext db, QuestionBank bank, IAuditTrail audit) : ICommandHandler<ImportQuestionsCommand, ImportResult>
{
    public async Task<Result<ImportResult>> Handle(ImportQuestionsCommand cmd, CancellationToken ct)
    {
        var code = await bank.CodeGeneratorAsync(ct);
        var created = new List<Question>();
        var errors = new List<ImportError>();
        var draftListening = 0;

        foreach (var row in cmd.Rows)
        {
            var content = row.Input.ToContent();
            if (await bank.CheckLessonsAsync(content, ct) is { IsFailure: true } lessons)
            {
                errors.Add(new ImportError(row.Row, [lessons.Error.Message]));
                continue;
            }
            var listening = content.Skill == JapaneseSkill.Listening;
            var q = Question.Create(code(content.Level, content.Skill), content, bank.Me, cmd.Submit && !listening);
            if (q.IsFailure)
            {
                errors.Add(new ImportError(row.Row, [q.Error.Message]));
                continue;
            }
            if (listening) draftListening++;
            created.Add(q.Value);
        }

        if (created.Count > 0)
        {
            db.Questions.AddRange(created);
            await db.SaveChangesAsync(ct);
            audit.Record(AuditCategory.Data, AuditSeverity.Info, "Nhập câu hỏi từ Excel", $"{created.Count} câu",
                errors.Count > 0 ? $"{errors.Count} dòng lỗi" : null);
        }
        return new ImportResult(await bank.ToDtosAsync(created, ct), errors, draftListening);
    }
}
