using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.Work.Application.Tasks.DeleteTask;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.Modules.Work.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Infrastructure.Services;

/// <summary>Thùng rác cho task (CV-01.E6): khôi phục = bỏ cờ xóa (checklist, bình luận giữ nguyên); xóa hẳn = xóa dòng <c>work.tasks</c>.</summary>
internal sealed class TaskTrashHandler(WorkDbContext db) : ITrashHandler
{
    public string Kind => DeleteTaskHandler.TrashKind;

    public async Task<Result<TrashRestoreOutcome>> RestoreAsync(Guid entityId, CancellationToken ct)
    {
        var id = new WorkTaskId(entityId);
        var task = await db.Tasks.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter]).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return new TrashRestoreOutcome(false);
        if (!db.IsSoftDeleted(task)) return new TrashRestoreOutcome(true, "Task đang hoạt động — không cần khôi phục");
        db.Undelete(task);
        return new TrashRestoreOutcome(true);
    }

    public async Task PurgeAsync(Guid entityId, CancellationToken ct)
    {
        var id = new WorkTaskId(entityId);
        await db.Tasks.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter])
            .Where(t => t.Id == id && EF.Property<bool>(t, ShadowColumns.IsDeleted))
            .ExecuteDeleteAsync(ct);
    }
}
