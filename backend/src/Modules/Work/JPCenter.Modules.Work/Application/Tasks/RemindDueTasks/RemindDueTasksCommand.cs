using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Tasks.RemindDueTasks;

/// <summary>
/// CV-06 — job 07:30 hằng ngày: mỗi người làm nhận 1 thông báo gom "N việc hôm nay, M việc quá hạn" (task chưa xong). Không có → không gửi.
/// Trả số người được nhắc.
/// </summary>
public sealed record RemindDueTasksCommand : ICommand<int>;

public sealed class RemindDueTasksHandler(IWorkDbContext db, WorkNotices notices, IClock clock) : ICommandHandler<RemindDueTasksCommand, int>
{
    public async Task<Result<int>> Handle(RemindDueTasksCommand cmd, CancellationToken ct)
    {
        var today = clock.Today;
        var due = await db.Tasks.AsNoTracking()
            .Where(t => t.Status != WorkTaskStatus.Done && t.Due != null && t.Due <= today)
            .Select(t => new { t.AssigneeId, t.Due })
            .ToListAsync(ct);
        var people = 0;
        foreach (var g in due.GroupBy(t => t.AssigneeId))
        {
            var todayCount = g.Count(t => t.Due == today);
            var overdue = g.Count(t => t.Due < today);
            var parts = new List<string>();
            if (todayCount > 0) parts.Add($"{todayCount} việc hạn hôm nay");
            if (overdue > 0) parts.Add($"{overdue} việc quá hạn");
            notices.ToEmployees([g.Key], "Nhắc việc hôm nay", string.Join(", ", parts), overdue > 0 ? "warning" : "info", WorkNotices.MyTasksPage,
                "work.task.reminder");
            people++;
        }
        if (people > 0) await db.SaveChangesAsync(ct);
        return people;
    }
}
