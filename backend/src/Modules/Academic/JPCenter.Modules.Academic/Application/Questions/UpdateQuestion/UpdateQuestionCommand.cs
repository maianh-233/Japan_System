using JPCenter.Application.Common.Files;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Questions.UpdateQuestion;

/// <summary>
/// NH-03 — sửa câu hỏi (phạm vi own: câu mình soạn). Câu đã duyệt → chờ duyệt; ngừng dùng → không sửa được.
/// Đổi khỏi nghe hiểu → xóa file nghe trong kho nếu không câu nào khác dùng chung (nhân bản dùng chung file).
/// </summary>
public sealed record UpdateQuestionCommand(Guid Id, QuestionInput Input, uint RowVersion) : ICommand<QuestionDto>;

public sealed class UpdateQuestionHandler(IAcademicDbContext db, QuestionBank bank, IFileStorage storage) : ICommandHandler<UpdateQuestionCommand, QuestionDto>
{
    public async Task<Result<QuestionDto>> Handle(UpdateQuestionCommand cmd, CancellationToken ct)
    {
        var q = await bank.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct);
        if (q is null) return QuestionErrors.NotFound;
        ((DbContext)db).Entry(q).Property("xmin").OriginalValue = cmd.RowVersion;

        var content = cmd.Input.ToContent();
        if (await bank.CheckLessonsAsync(content, ct) is { IsFailure: true } lessons) return lessons.Error;
        if (q.Edit(content, out var removedAudio) is { IsFailure: true } edited) return edited.Error;

        await db.SaveChangesAsync(ct);
        await AudioFiles.DeleteIfUnusedAsync(db, storage, removedAudio, ct);
        return await bank.ToDtoAsync(q, ct);
    }
}
