using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Tasks.Common;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Tasks.GetTasks;

/// <summary>Tập task đang xem</summary>
public enum TaskScope
{
    /// <summary>Tất cả trong phạm vi <c>work.tasks.view</c> (phạm vi own = task tôi dính tới)</summary>
    All,
    /// <summary>Tôi làm (= Việc của tôi, CV-03)</summary>
    Mine,
    /// <summary>Tôi giao</summary>
    Assigned,
    /// <summary>Tôi theo dõi</summary>
    Watching,
    /// <summary>Task của cấp dưới (CV-04 — không cần mã quyền)</summary>
    Team,
}

/// <summary>
/// CV-02 / CV-03 / CV-04 — danh sách task, sắp theo ưu tiên rồi hạn, kèm thống kê. <paramref name="OnlyMine"/> (tab cấp dưới): chỉ task tôi giao.
/// "Việc của tôi" bỏ task đã xong quá <see cref="RecentDoneDays"/> ngày.
/// </summary>
public sealed record GetTasksQuery(TaskScope Scope, Guid? AssigneeId, TaskPriority? Priority, WorkTaskStatus? Status, bool OnlyMine = false)
    : IQuery<TaskListDto>;

public sealed class GetTasksHandler(IWorkDbContext db, TaskDesk desk, WorkPeople people, ICurrentUser user, IClock clock)
    : IQueryHandler<GetTasksQuery, TaskListDto>
{
    public const int RecentDoneDays = 14;
    public static readonly Error ViewForbidden = Error.Forbidden("work.task.view_forbidden", "Bạn không có quyền xem task toàn hệ thống");

    public async Task<Result<TaskListDto>> Handle(GetTasksQuery q, CancellationToken ct)
    {
        var me = people.MeId ?? Guid.Empty;
        IQueryable<WorkTask> query = db.Tasks.AsNoTracking();
        List<WorkTask> rows;
        switch (q.Scope)
        {
            case TaskScope.Mine:
                rows = await query.Where(t => t.AssigneeId == me).ToListAsync(ct);
                var since = clock.UtcNow.AddDays(-RecentDoneDays);
                rows = rows.Where(t => !t.IsDone || t.CompletedAt >= since).ToList();
                break;
            case TaskScope.Assigned:
                rows = await query.Where(t => t.CreatorId == me).ToListAsync(ct);
                break;
            case TaskScope.Watching:
                rows = await query.Where(t => t.WatcherIds.Contains(me)).ToListAsync(ct);
                break;
            case TaskScope.Team:
                var reports = (await desk.ReportIdsAsync(ct)).ToList();
                rows = await query.Where(t => reports.Contains(t.AssigneeId) && (!q.OnlyMine || t.CreatorId == me)).ToListAsync(ct);
                break;
            default:
                var perm = user.Permission(TaskDesk.Page, PermissionAction.View);
                if (perm is null) return ViewForbidden;
                if (perm.Scope == PermissionScope.Own)
                    rows = await query.Where(t => t.AssigneeId == me || t.CreatorId == me || t.WatcherIds.Contains(me)).ToListAsync(ct);
                else if (user.AllowedBranches(TaskDesk.Page, PermissionAction.View) is { } branches)
                {
                    var ids = branches.Select(b => new BranchId(b)).ToList();
                    rows = await query.Where(t => ids.Contains(t.BranchId)).ToListAsync(ct);
                }
                else rows = await query.ToListAsync(ct);
                break;
        }

        var filtered = rows
            .Where(t => q.AssigneeId is not { } a || t.AssigneeId == a)
            .Where(t => q.Priority is not { } p || t.Priority == p)
            .Where(t => q.Status is not { } s || t.Status == s)
            .ToList();
        var stats = TaskDesk.Stats(filtered, clock.Today, clock.UtcNow);
        return new TaskListDto(await desk.ToDtosAsync(TaskDesk.Sort(filtered).ToList(), ct), stats);
    }
}
