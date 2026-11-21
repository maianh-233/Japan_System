using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Questions.ChangeQuestionStatus;

/// <summary>Bước trong quy trình duyệt (NH-05, NH-06)</summary>
public enum QuestionAction
{
    /// <summary>Gửi duyệt (nháp → chờ duyệt) — quyền update</summary>
    Submit,
    /// <summary>Duyệt — quyền approve, không tự duyệt</summary>
    Approve,
    /// <summary>Trả lại (cần lý do) — quyền approve</summary>
    Return,
    /// <summary>Ngừng dùng (cần lý do) — quyền approve</summary>
    Retire,
    /// <summary>Mở lại → chờ duyệt — quyền approve</summary>
    Reopen,
}

/// <summary>
/// NH-05 / NH-06 — đổi trạng thái 1 hoặc nhiều câu trong 1 request. Câu không làm được (sai trạng thái, tự duyệt, ngoài phạm vi, nghe hiểu
/// thiếu file) → bỏ qua kèm lý do, không làm hỏng cả lô (NH-05.E2). Lưu 1 lần.
/// </summary>
public sealed record ChangeQuestionStatusCommand(QuestionAction Action, IReadOnlyList<Guid> Ids, string? Note) : ICommand<BulkResult>;

public sealed class ChangeQuestionStatusValidator : AbstractValidator<ChangeQuestionStatusCommand>
{
    public const int MaxIds = 500;

    public ChangeQuestionStatusValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().WithMessage("Chọn câu hỏi").Must(ids => ids.Count <= MaxIds).WithMessage($"Tối đa {MaxIds} câu mỗi lần");
        RuleFor(x => x.Note).NotEmpty().WithMessage("Cần ghi lý do").When(x => x.Action is QuestionAction.Return or QuestionAction.Retire);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class ChangeQuestionStatusHandler(IAcademicDbContext db, QuestionBank bank, IClock clock, IAuditTrail audit)
    : ICommandHandler<ChangeQuestionStatusCommand, BulkResult>
{
    public async Task<Result<BulkResult>> Handle(ChangeQuestionStatusCommand cmd, CancellationToken ct)
    {
        var permission = cmd.Action == QuestionAction.Submit ? PermissionAction.Update : PermissionAction.Approve;
        if (!bank.Can(permission)) return Error.Forbidden("system.permission.denied", "Bạn không có quyền thực hiện thao tác này");
        var ownOnly = bank.OwnOnly(permission);
        var me = bank.Me;
        var now = clock.UtcNow;

        var ids = cmd.Ids.Distinct().Select(id => new QuestionId(id)).ToList();
        var found = await db.Questions.Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var done = new List<Question>();
        var skipped = new List<BulkSkipped>();

        foreach (var id in ids)
        {
            if (!found.TryGetValue(id, out var q) || (ownOnly && q.AuthorId != me))
            {
                skipped.Add(new BulkSkipped(id.Value, null, QuestionErrors.NotFound.Message));
                continue;
            }
            var r = cmd.Action switch
            {
                QuestionAction.Submit => q.Submit(),
                QuestionAction.Approve => q.Approve(me, now),
                QuestionAction.Return => q.Return(me, now, cmd.Note ?? ""),
                QuestionAction.Retire => q.Retire(me, now, cmd.Note ?? ""),
                _ => q.Reopen(),
            };
            if (r.IsSuccess) done.Add(q);
            else skipped.Add(new BulkSkipped(q.Id.Value, q.Code, r.Error.Message));
        }

        if (done.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            audit.Record(AuditCategory.Data, cmd.Action == QuestionAction.Retire ? AuditSeverity.Warning : AuditSeverity.Info, ActionText(cmd.Action),
                done.Count == 1 ? done[0].Code : $"{done.Count} câu hỏi", cmd.Note);
        }
        return new BulkResult(await bank.ToDtosAsync(done, ct), skipped);
    }

    private static string ActionText(QuestionAction action) => action switch
    {
        QuestionAction.Submit => "Gửi duyệt câu hỏi",
        QuestionAction.Approve => "Duyệt câu hỏi",
        QuestionAction.Return => "Trả lại câu hỏi",
        QuestionAction.Retire => "Ngừng dùng câu hỏi",
        _ => "Mở lại câu hỏi",
    };
}
