using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Tasks.Common;

/// <summary>
/// Quyền trên 1 task (quy tắc vàng 2, CV-02): người dính tới task (làm / giao / theo dõi) và quản lý của người làm (cây quản lý, CV-04) luôn xem,
/// bình luận, đổi trạng thái được; ngoài ra theo <c>work.tasks.*</c> + phạm vi chi nhánh của người làm (phạm vi own = chỉ task mình dính tới).
/// Sửa nội dung: người giao / <c>update</c> / quản lý người làm · Xóa: người giao / <c>delete</c>.
/// </summary>
public sealed class TaskDesk(IWorkDbContext db, ICurrentUser user, WorkPeople people, ILeadLookup leads, IClassLookup classes,
    IEnrollmentLookup students)
{
    public const string Page = "work.tasks";
    private HashSet<Guid>? _reports;

    /// <summary>Có quyền trang với phạm vi chi nhánh (không tính own — own chỉ là task mình dính tới)</summary>
    public bool InBranch(PermissionAction action, Guid branchId) =>
        user.Permission(Page, action) is { Scope: not PermissionScope.Own } && user.InBranch(Page, action, branchId);

    public async Task<HashSet<Guid>> ReportIdsAsync(CancellationToken ct) =>
        _reports ??= (await people.MyReportsAsync(ct)).Select(r => r.Employee.Id).ToHashSet();

    public async Task<bool> CanViewAsync(WorkTask t, CancellationToken ct) =>
        Involved(t) || InBranch(PermissionAction.View, t.BranchId.Value) || (await ReportIdsAsync(ct)).Contains(t.AssigneeId);

    /// <summary>Đổi trạng thái, checklist, bình luận</summary>
    public async Task<bool> CanMoveAsync(WorkTask t, CancellationToken ct) =>
        Involved(t) || InBranch(PermissionAction.Update, t.BranchId.Value) || (await ReportIdsAsync(ct)).Contains(t.AssigneeId);

    public async Task<bool> CanEditAsync(WorkTask t, CancellationToken ct) =>
        t.CreatorId == people.MeId || InBranch(PermissionAction.Update, t.BranchId.Value) || (await ReportIdsAsync(ct)).Contains(t.AssigneeId);

    public bool CanDelete(WorkTask t) => t.CreatorId == people.MeId || InBranch(PermissionAction.Delete, t.BranchId.Value);

    public bool Involved(WorkTask t) => people.MeId is { } me && t.Involves(me);

    /// <summary>Task (theo dõi để sửa) người đang gọi được xem; không thấy / ngoài phạm vi → null (404)</summary>
    public async Task<WorkTask?> FindAsync(Guid id, CancellationToken ct)
    {
        var t = await db.Tasks.FirstOrDefaultAsync(x => x.Id == new WorkTaskId(id), ct);
        return t is not null && await CanViewAsync(t, ct) ? t : null;
    }

    /// <summary>
    /// Người làm hợp lệ: nhân sự đang làm việc (không nghỉ việc), và trong phạm vi <c>work.tasks.{action}</c> hoặc là cấp dưới của tôi.
    /// </summary>
    public async Task<EmployeeSummary?> AssigneeAsync(Guid id, PermissionAction action, bool reportsOnly, CancellationToken ct)
    {
        var e = await people.FindAsync(id, ct);
        if (e is null || e.HasQuit) return null;
        if ((await ReportIdsAsync(ct)).Contains(id)) return e;
        return !reportsOnly && InBranch(action, e.BranchId) ? e : null;
    }

    /// <summary>CV-01.E4 — nhãn đối tượng gắn task; không thấy → null</summary>
    public async Task<string?> LinkLabelAsync(TaskLinkInput link, CancellationToken ct)
    {
        switch (link.Type)
        {
            case TaskLinkType.Lead:
                return (await leads.FindManyAsync([link.Id], ct)).TryGetValue(link.Id, out var lead) ? $"Lead {lead.FullName}" : null;
            case TaskLinkType.Class:
                return await classes.FindAsync(link.Id, ct) is { } c ? $"Lớp {c.Code}" : null;
            default:
                return (await students.NamesAsync([link.Id], ct)).TryGetValue(link.Id, out var name) ? $"HV {name}" : null;
        }
    }

    /// <summary>Bộ sinh mã TK-0001… (tính cả task trong Thùng rác để không trùng) — gọi nhiều lần trong 1 request vẫn tăng dần</summary>
    public async Task<Func<string>> CodesAsync(CancellationToken ct)
    {
        var codes = await db.Tasks.IgnoreQueryFilters().AsNoTracking().Select(t => t.Code).ToListAsync(ct);
        var next = codes.Select(c => c.StartsWith("TK-", StringComparison.Ordinal) && int.TryParse(c[3..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return () => $"TK-{++next:0000}";
    }

    public async Task<IReadOnlyList<TaskDto>> ToDtosAsync(IReadOnlyList<WorkTask> tasks, CancellationToken ct)
    {
        if (tasks.Count == 0) return [];
        var ids = tasks.Select(t => t.Id).ToList();
        var created = await db.Tasks.IgnoreQueryFilters().AsNoTracking().Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, At = EF.Property<DateTimeOffset>(t, "CreatedAt") }).ToDictionaryAsync(x => x.Id, x => x.At, ct);
        var result = new List<TaskDto>(tasks.Count);
        foreach (var t in tasks)
            result.Add(new TaskDto(t.Id.Value, t.Code, t.Title, t.Description, t.AssigneeId, t.CreatorId, t.WatcherIds, t.Priority, t.Status, t.Due,
                t.Repeat, t.LinkType is { } lt ? new TaskLinkDto(lt, t.LinkId!.Value, t.LinkLabel!) : null,
                t.Checklist.OrderBy(i => i.Order).Select(i => new ChecklistItemDto(i.Id, i.Text, i.Done)).ToList(),
                t.Comments.OrderBy(c => c.At).Select(c => new TaskCommentDto(c.At, c.ByName, c.By, c.Text)).ToList(),
                created.GetValueOrDefault(t.Id), t.CompletedAt, t.BranchId.Value, await CanEditAsync(t, ct), CanDelete(t), await CanMoveAsync(t, ct)));
        return result;
    }

    public async Task<TaskDto> ToDtoAsync(WorkTask t, CancellationToken ct) => (await ToDtosAsync([t], ct))[0];

    public static TaskStatsDto Stats(IEnumerable<WorkTask> tasks, DateOnly today, DateTimeOffset now)
    {
        var list = tasks.ToList();
        return new TaskStatsDto(
            list.Count(t => !t.IsDone && t.Due < today),
            list.Count(t => !t.IsDone && t.Due == today),
            list.Count(t => t.Status == WorkTaskStatus.Review),
            list.Count(t => t.IsDone && t.CompletedAt >= now.AddDays(-7)));
    }

    /// <summary>Ưu tiên cao trước, rồi hạn gần trước (không hạn cuối cùng)</summary>
    public static IEnumerable<WorkTask> Sort(IEnumerable<WorkTask> tasks) =>
        tasks.OrderByDescending(t => t.Priority).ThenBy(t => t.Due ?? DateOnly.MaxValue).ThenBy(t => t.Code);
}
