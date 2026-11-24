using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Application.Common.Time;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Reporting.Application.Common;
using JPCenter.Modules.Reporting.Domain.Alerts;
using JPCenter.Modules.Reporting.Domain.Metrics;
using JPCenter.Modules.Reporting.Domain.Periods;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Reporting.Application.Dashboard.GetDashboard;

/// <summary>BC-01 — 4 thẻ chỉ số + "Cần xử lý" của bảng điều khiển (buổi học hôm nay, đơn chờ duyệt dùng API sẵn có).</summary>
public sealed record GetDashboardQuery(Guid? BranchId) : IQuery<DashboardDto>;

/// <param name="LeadGrowth">% lead mới tháng này so với tháng trước (số nguyên); null khi tháng trước = 0 (BC-01.E1)</param>
/// <param name="RevenueGrowth">% thực thu (1 chữ số thập phân); null khi tháng trước = 0</param>
/// <param name="Debt">Tổng còn phải thu</param>
/// <param name="Overdue">Trong đó quá hạn</param>
public sealed record DashboardDto(int Studying, int OngoingClasses, int LeadsThisMonth, int LeadsPrevMonth, int? LeadGrowth, decimal CollectedThisMonth,
    decimal CollectedPrevMonth, decimal? RevenueGrowth, decimal Debt, decimal Overdue, IReadOnlyList<DashboardAlertDto> Alerts);

/// <param name="Kind">lead_sla_overdue · unconfirmed_sessions · missing_teacher_logs · low_fill_classes · integration_error</param>
/// <param name="Tone">danger · warning</param>
public sealed record DashboardAlertDto(string Kind, string Tone, string Text, string Path);

public sealed class GetDashboardHandler(ReportingAccess access, ILeadReportSource leads, IEnrollmentReportSource enrollments,
    IAcademicReportSource academic, IFinanceReportSource finance, IIntegrationHealth integrations, IClock clock)
    : IQueryHandler<GetDashboardQuery, DashboardDto>
{
    public const string IntegrationsPage = "system.integrations";

    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery q, CancellationToken ct)
    {
        var branches = access.Branches(ReportingAccess.Overview, PermissionAction.View, q.BranchId);
        var period = ReportPeriod.Create(3, clock.Today).Value;
        var today = period.Today;

        var leadRows = await leads.LeadsCreatedAsync(branches, TimeText.At(period.PreviousMonth, TimeOnly.MinValue), ct);
        int LeadsIn(DateOnly month) => leadRows.Count(l => TimeText.DateOf(l.CreatedAt) is var d && d.Year == month.Year && d.Month == month.Month);
        var (leadsNow, leadsPrev) = (LeadsIn(period.CurrentMonth), LeadsIn(period.PreviousMonth));

        var enrollRows = await enrollments.EnrollmentsAsync(branches, today, ct);
        var studying = enrollRows.Where(e => e.Status == "studying").Select(e => e.StudentId).Distinct().Count();
        var classes = await academic.OpenClassesAsync(branches, ct);

        var cash = await finance.CashByMonthAsync(branches, period.PreviousMonth, ct);
        var (cashNow, cashPrev) = (cash.Where(c => c.Month == period.CurrentMonth).Sum(c => c.Collected),
            cash.Where(c => c.Month == period.PreviousMonth).Sum(c => c.Collected));
        var debts = await finance.DebtsAsync(branches, ct);

        // ---------- Cần xử lý ----------
        var slaOverdue = (await leads.OpenLeadsAsync(branches, null, ct)).Count(l => l.SlaOverdue);
        var sessions = await academic.SessionAlertsAsync(branches, today.AddDays(-DashboardAlert.UnconfirmedLookbackDays),
            today.AddDays(-DashboardAlert.MissingLogLookbackDays), ct);
        var filled = enrollRows.Where(e => e.Status is "studying" or "reserved").GroupBy(e => e.ClassId).ToDictionary(g => g.Key, g => g.Count());
        var lowFill = classes.Select(c => (c.Code, Rate: Ratio.Percent(filled.GetValueOrDefault(c.Id), c.MaxStudents)))
            .Where(c => DashboardAlert.IsLowFill(c.Rate)).OrderBy(c => c.Rate).ThenBy(c => c.Code, StringComparer.Ordinal).Select(c => c.Code).ToList();
        var failing = access.Can(IntegrationsPage, PermissionAction.View) ? await integrations.FailingAsync(ct) : []; // BC-01.E3

        var alerts = new[]
            {
                DashboardAlert.LeadSlaOverdue(slaOverdue), DashboardAlert.UnconfirmedSessions(sessions.Unconfirmed),
                DashboardAlert.MissingTeacherLogs(sessions.MissingLogs), DashboardAlert.LowFillClasses(lowFill),
            }
            .Concat(failing.Select(f => DashboardAlert.IntegrationError(f.Name, f.Error)))
            .OfType<DashboardAlert>()
            .Select(a => new DashboardAlertDto(Snake(a.Kind.ToString()), Snake(a.Tone.ToString()), a.Text, a.Path))
            .ToList();

        return new DashboardDto(studying, classes.Count(c => c.Status == "ongoing"), leadsNow, leadsPrev, Ratio.GrowthPercent(leadsNow, leadsPrev),
            cashNow, cashPrev, Ratio.Growth(cashNow, cashPrev), debts.Sum(d => d.Remaining), debts.Where(d => d.Overdue).Sum(d => d.Remaining), alerts);
    }

    private static string Snake(string name) => global::System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
}
