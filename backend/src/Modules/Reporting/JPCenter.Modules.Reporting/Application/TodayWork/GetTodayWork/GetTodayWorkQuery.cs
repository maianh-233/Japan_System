using System.Globalization;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Reporting.Application.Common;
using JPCenter.Modules.Reporting.Domain.TodayWork;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Reporting.Application.TodayWork.GetTodayWork;

/// <summary>
/// BC-02 — Việc hôm nay theo vai trò. Tab suy ra từ QUYỀN (<c>crm.leads</c>, <c>academic.sessions</c>, <c>finance.invoices</c>), phạm vi chi nhánh
/// = chi nhánh đang chọn ∩ phạm vi quyền nghiệp vụ. Không có quyền nghiệp vụ nào → danh sách rỗng (BC-02.E4).
/// </summary>
public sealed record GetTodayWorkQuery(Guid? BranchId) : IQuery<IReadOnlyList<WorkBoardDto>>;

/// <param name="Role">sale · academic · finance</param>
/// <param name="Count">Tổng số việc (hiện trên tab)</param>
public sealed record WorkBoardDto(string Role, string Label, int Count, IReadOnlyList<WorkGroupDto> Groups);

/// <param name="AllPath">"Xem tất cả"</param>
/// <param name="Empty">Câu khi không có việc</param>
public sealed record WorkGroupDto(string Key, string Label, IReadOnlyList<WorkItemDto> Items, string AllPath, string Empty);

/// <param name="Meta">Nhãn ngắn bên phải ("quá 2 giờ", "hạn hôm nay")</param>
/// <param name="Tone">danger · warning · info</param>
public sealed record WorkItemDto(string Key, string Title, string? Subtitle, string Meta, string Tone, string Path);

public sealed class GetTodayWorkHandler(ReportingAccess access, ILeadReportSource leads, IAcademicReportSource academic, IFinanceReportSource finance,
    IHrRequestInbox hrInbox, IEnrollmentLookup students, IClassLookup classes, IClock clock)
    : IQueryHandler<GetTodayWorkQuery, IReadOnlyList<WorkBoardDto>>
{
    public const string Leads = "crm.leads";
    public const string Sessions = "academic.sessions";
    public const string Requests = "academic.requests";
    public const string Invoices = "finance.invoices";
    public const string Reconciliation = "finance.reconciliation";

    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<Result<IReadOnlyList<WorkBoardDto>>> Handle(GetTodayWorkQuery q, CancellationToken ct)
    {
        var selected = access.Branches(ReportingAccess.Overview, PermissionAction.View, q.BranchId);
        var now = clock.LocalNow;
        var boards = new List<WorkBoardDto>();
        if (access.Can(Leads, PermissionAction.View)) boards.Add(await SaleAsync(access.Narrow(selected, Leads, PermissionAction.View), now, ct));
        if (access.Can(Sessions, PermissionAction.View)) boards.Add(await AcademicAsync(selected, now, ct));
        if (access.Can(Invoices, PermissionAction.View)) boards.Add(await FinanceAsync(selected, now, ct));
        return boards;
    }

    // ================= Sale =================
    private async Task<WorkBoardDto> SaleAsync(IReadOnlyCollection<Guid>? branches, DateTime now, CancellationToken ct)
    {
        var ownOnly = access.OwnOnly(Leads, PermissionAction.View);
        var open = ReportingAccess.Any(branches) && (!ownOnly || access.EmployeeId is not null)
            ? await leads.OpenLeadsAsync(branches, ownOnly ? access.EmployeeId : null, ct)
            : [];

        var sla = open.Where(l => l.Status == "new" && l.LastContactAt is null)
            .Select(l => (Lead: l, Urgency: WorkRules.LeadSla(Local(l.CreatedAt), l.SlaMinutes, now)))
            .Where(x => x.Urgency is not null)
            .Select(x => new WorkItem($"sla:{x.Lead.Id}", x.Lead.FullName,
                Join(x.Lead.Phone, ownOnly ? null : x.Lead.AssigneeName ?? "Chưa giao Sale"), x.Urgency!, LeadPath(x.Lead.Id), Local(x.Lead.CreatedAt)));
        var callback = open.Where(l => l.NextFollowUpAt is not null)
            .Select(l => (Lead: l, At: Local(l.NextFollowUpAt!.Value)))
            .Select(x => (x.Lead, x.At, Urgency: WorkRules.FollowUp(x.At, now)))
            .Where(x => x.Urgency is not null)
            .Select(x => new WorkItem($"cb:{x.Lead.Id}", x.Lead.FullName, Join(x.Lead.Phone, x.Lead.Interest, ownOnly ? null : x.Lead.AssigneeName),
                x.Urgency!, LeadPath(x.Lead.Id), x.At));

        return Board("sale", "Sale",
            Group("sla", "Lead đã / sắp quá SLA", sla, "/admin/crm/leads", "Không có lead mới nào sắp quá hạn liên hệ."),
            Group("callback", "Lead cần gọi lại hôm nay", callback, "/admin/crm/leads", "Không có lịch hẹn gọi lại hôm nay."));
    }

    // ================= Giáo vụ =================
    private async Task<WorkBoardDto> AcademicAsync(IReadOnlyCollection<Guid>? selected, DateTime now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now);
        var branches = access.Narrow(selected, Sessions, PermissionAction.View);
        var sessions = ReportingAccess.Any(branches)
            ? await academic.SessionsWithoutAttendanceAsync(branches, today.AddDays(-WorkRules.AttendanceLookbackDays), today, ct)
            : [];
        var noAttendance = sessions
            .Select(s => (Session: s, Urgency: WorkRules.MissingAttendance(s.Date, s.Start, now)))
            .Where(x => x.Urgency is not null)
            .Select(x => new WorkItem($"att:{x.Session.SessionId}", x.Session.ClassCode,
                $"{DayName(x.Session.Date)} {x.Session.Date.ToString("dd/MM", Vi)} · {x.Session.Start:HH\\:mm}–{x.Session.End:HH\\:mm}"
                + (x.Session.Exam ? " · Buổi thi" : ""),
                x.Urgency!, $"/admin/academic/classes/{x.Session.ClassId}?tab=attendance", x.Session.Date.ToDateTime(x.Session.Start)));

        var requestBranches = access.Narrow(selected, Requests, PermissionAction.Approve);
        var classRequests = access.Can(Requests, PermissionAction.Approve) && ReportingAccess.Any(requestBranches)
            ? await academic.PendingRequestsAsync(requestBranches, ct)
            : [];
        var hrRequests = ReportingAccess.Any(selected) ? await hrInbox.PendingForMeAsync(selected, ct) : [];
        var pending = classRequests
            .Select(r => new WorkItem($"inbox:{r.Kind}:{r.Id}", r.Who,
                (r.Kind == "cancel" ? "GV xin hủy buổi" : "HV xin nghỉ học") + (r.ClassCode is { } c ? $" · {c}" : ""),
                WorkRules.PendingRequest(Local(r.RequestedAt), r.SessionStart, now), "/admin/academic/requests", Local(r.RequestedAt)))
            .Concat(hrRequests.Select(r => new WorkItem($"inbox:hr:{r.Id}", r.EmployeeName, $"Đơn từ nhân viên · {r.Code}",
                WorkRules.PendingRequest(Local(r.RequestedAt), null, now), "/admin/academic/requests", Local(r.RequestedAt))));

        return Board("academic", "Giáo vụ",
            Group("attendance", "Buổi học chưa điểm danh", noAttendance, "/admin/academic/sessions",
                $"Mọi buổi học {WorkRules.AttendanceLookbackDays} ngày qua đã điểm danh."),
            Group("requests", "Đơn chờ duyệt", pending, "/admin/academic/requests", "Không còn đơn nào chờ bạn duyệt."));
    }

    // ================= Kế toán =================
    private async Task<WorkBoardDto> FinanceAsync(IReadOnlyCollection<Guid>? selected, DateTime now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now);
        var branches = access.Narrow(selected, Invoices, PermissionAction.View);
        var debts = ReportingAccess.Any(branches) ? await finance.DebtsAsync(branches, ct) : [];
        var dueRows = debts.Select(d => (Debt: d, Urgency: WorkRules.InvoiceDue(d.DueDate, today))).Where(x => x.Urgency is not null).ToList();
        var names = await students.NamesAsync(dueRows.Select(x => x.Debt.StudentId).Distinct().ToList(), ct);
        var codes = await classes.FindManyAsync(dueRows.Select(x => x.Debt.ClassId).Distinct().ToList(), ct);
        var due = dueRows.Select(x => new WorkItem($"inv:{x.Debt.InvoiceId}", $"{x.Debt.Code} — {names.GetValueOrDefault(x.Debt.StudentId) ?? "—"}",
            Join(codes.TryGetValue(x.Debt.ClassId, out var c) ? c.Code : null, $"còn nợ {Money(x.Debt.Remaining)}"), x.Urgency!,
            $"/admin/finance/invoices?open={x.Debt.InvoiceId}", x.Debt.DueDate.ToDateTime(TimeOnly.MinValue)));

        var groups = new List<WorkGroupDto>
        {
            Group("due", $"Hóa đơn đến hạn ({WorkRules.DueSoonDays} ngày tới) & quá hạn", due, "/admin/finance/debts", "Không có hóa đơn nào đến hạn."),
        };
        if (access.Can(Reconciliation, PermissionAction.View)) // BC-02.E3
        {
            var txBranches = access.Narrow(selected, Reconciliation, PermissionAction.View);
            var txns = ReportingAccess.Any(txBranches) ? await finance.UnmatchedTransactionsAsync(txBranches, ct) : [];
            groups.Add(Group("txn", "Giao dịch VietQR chưa khớp",
                txns.Select(t => new WorkItem($"txn:{t.Id}", $"{Money(t.Amount)} — {t.Payer}", t.Content, WorkRules.UnmatchedTransaction(Local(t.At), now),
                    "/admin/finance/reconciliation", Local(t.At))),
                "/admin/finance/reconciliation", "Mọi giao dịch đã được khớp."));
        }
        return new WorkBoardDto("finance", "Kế toán", groups.Sum(g => g.Items.Count), groups);
    }

    private static WorkBoardDto Board(string role, string label, params WorkGroupDto[] groups) =>
        new(role, label, groups.Sum(g => g.Items.Count), groups);

    private static WorkGroupDto Group(string key, string label, IEnumerable<WorkItem> items, string allPath, string empty) =>
        new(key, label, WorkItem.Order(items).Select(ToDto).ToList(), allPath, empty);

    private static WorkItemDto ToDto(WorkItem i) =>
        new(i.Key, i.Title, i.Subtitle, i.Urgency.Meta, global::System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(i.Urgency.Tone.ToString()), i.Path);

    private static DateTime Local(DateTimeOffset at) => at.ToOffset(Vn).DateTime;

    private static string LeadPath(Guid id) => $"/admin/crm/leads?open={id}";

    private static string? Join(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return text.Length == 0 ? null : text;
    }

    /// <summary>Giống <c>formatCurrency</c> của frontend: "1.500.000 ₫"</summary>
    private static string Money(decimal amount) => $"{amount.ToString("N0", Vi)} ₫";

    /// <summary>Thứ viết tắt như dayjs locale vi (<c>dd</c>): CN, T2 … T7</summary>
    private static string DayName(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? "CN" : $"T{(int)d.DayOfWeek + 1}";
}
