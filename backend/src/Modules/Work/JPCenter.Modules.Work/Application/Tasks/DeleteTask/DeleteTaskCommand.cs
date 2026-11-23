using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Contracts.IntegrationEvents;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Tasks.Common;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Application.Tasks.DeleteTask;

/// <summary>CV-01.E6 — xóa task (<c>work.tasks.delete</c> hoặc người giao) vào Thùng rác (<c>work.task</c>).</summary>
public sealed record DeleteTaskCommand(Guid Id) : ICommand;

public sealed class DeleteTaskHandler(IWorkDbContext db, TaskDesk desk, IIntegrationEventPublisher events, ICurrentUser user, IClock clock)
    : ICommandHandler<DeleteTaskCommand>
{
    public const string TrashKind = "work.task";

    public async Task<Result> Handle(DeleteTaskCommand cmd, CancellationToken ct)
    {
        var t = await desk.FindAsync(cmd.Id, ct);
        if (t is null) return WorkTaskErrors.NotFound;
        if (!desk.CanDelete(t)) return WorkTaskErrors.Forbidden;
        db.Tasks.Remove(t);
        events.Publish(new ItemTrashed(TrashKind, t.Id.Value, $"{t.Code} · {t.Title}", t.Due is { } d ? $"Task · hạn {d:dd/MM/yyyy}" : "Task",
            t.BranchId.Value, user.AccountId ?? Guid.Empty, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
