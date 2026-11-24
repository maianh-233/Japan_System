using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Hr.Application.Recruitment.Jobs.DeleteJob;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Infrastructure.Services;

/// <summary>Thùng rác cho tin tuyển dụng (TD-01): khôi phục = bỏ cờ xóa; xóa hẳn = xóa dòng <c>hr.jobs</c> (tin chưa có ứng viên).</summary>
internal sealed class JobTrashHandler(HrDbContext db) : ITrashHandler
{
    public string Kind => DeleteJobHandler.TrashKind;

    public async Task<Result<TrashRestoreOutcome>> RestoreAsync(Guid entityId, CancellationToken ct)
    {
        var id = new JobId(entityId);
        var job = await db.Jobs.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter]).FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job is null) return new TrashRestoreOutcome(false);
        if (!db.IsSoftDeleted(job)) return new TrashRestoreOutcome(true, "Tin đang hiển thị — không cần khôi phục");
        db.Undelete(job);
        // DbContext riêng của Hr — System chỉ lưu bảng trash_items của nó
        await db.SaveChangesAsync(ct);
        return new TrashRestoreOutcome(true);
    }

    public async Task PurgeAsync(Guid entityId, CancellationToken ct)
    {
        var id = new JobId(entityId);
        await db.Jobs.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter])
            .Where(j => j.Id == id && EF.Property<bool>(j, ShadowColumns.IsDeleted))
            .ExecuteDeleteAsync(ct);
    }
}
