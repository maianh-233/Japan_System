using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Hr.Contracts.IntegrationEvents;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Tasks.EventHandlers;

/// <summary>
/// CV-06 / NS-07.E5 — nhân viên nghỉ việc: task chưa xong của họ GIỮ NGUYÊN, người giao (còn làm việc) nhận thông báo để giao lại.
/// Chạy lại → gửi lại thông báo (chấp nhận được, hiếm).
/// </summary>
public sealed class NotifyTaskCreatorsOnQuit(IWorkDbContext db, WorkNotices notices) : IIntegrationEventHandler<EmployeeQuit>
{
    public async Task Handle(EmployeeQuit e, CancellationToken ct)
    {
        var open = await db.Tasks.AsNoTracking()
            .Where(t => t.AssigneeId == e.EmployeeId && t.Status != WorkTaskStatus.Done && t.CreatorId != e.EmployeeId)
            .Select(t => new { t.CreatorId, t.Code })
            .ToListAsync(ct);
        if (open.Count == 0) return;
        foreach (var g in open.GroupBy(t => t.CreatorId))
            notices.ToEmployees([g.Key], $"{e.FullName} đã nghỉ việc — cần giao lại việc",
                $"{g.Count()} task chưa xong: {string.Join(", ", g.Select(t => t.Code).Take(5))}{(g.Count() > 5 ? "…" : "")}", "warning",
                WorkNotices.TasksPage, "work.task.assignee_quit");
        await db.SaveChangesAsync(ct);
    }
}
