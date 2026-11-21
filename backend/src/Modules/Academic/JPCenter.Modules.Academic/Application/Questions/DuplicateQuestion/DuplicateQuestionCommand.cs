using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Questions.DuplicateQuestion;

/// <summary>NH-07 — nhân bản câu xem được thành câu nháp mới của người bấm (dùng chung file nghe, thống kê = 0).</summary>
public sealed record DuplicateQuestionCommand(Guid Id) : ICommand<QuestionDto>;

public sealed class DuplicateQuestionHandler(IAcademicDbContext db, QuestionBank bank) : ICommandHandler<DuplicateQuestionCommand, QuestionDto>
{
    public async Task<Result<QuestionDto>> Handle(DuplicateQuestionCommand cmd, CancellationToken ct)
    {
        var src = await bank.Visible().AsNoTracking().FirstOrDefaultAsync(x => x.Id == new QuestionId(cmd.Id), ct);
        if (src is null) return QuestionErrors.NotFound;

        var code = await bank.CodeGeneratorAsync(ct);
        var copy = src.Duplicate(code(src.Level, src.Skill), bank.Me);
        db.Questions.Add(copy);
        await db.SaveChangesAsync(ct);
        return await bank.ToDtoAsync(copy, ct);
    }
}
