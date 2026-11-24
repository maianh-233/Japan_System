using JPCenter.Application.Common.Time;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Reporting.Domain.Branches;
using JPCenter.Modules.Reporting.Domain.Metrics;
using JPCenter.Modules.Reporting.Domain.Periods;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.System.Contracts;

namespace JPCenter.Modules.Reporting.Application.Reports.Common;

/// <summary>
/// Dựng báo cáo BC-03 → 06 từ các nguồn Contracts (ADR 0004). Chỉ cộng / chia / xếp hạng — quy tắc nghiệp vụ (SLA, tuổi nợ, trạng thái tiền)
/// đã tính ở module sở hữu.
/// </summary>
public sealed class ReportBuilder(ILeadReportSource leads, IEnrollmentReportSource enrollments, IAcademicReportSource academic,
    IFinanceReportSource finance, IEmployeeDirectory employees, IBranchDirectory branchDirectory)
{
    public const string SalesDepartment = "sales";

    private static readonly string[] FunnelOrder = ["new", "consulting", "test_scheduled", "won", "lost"];
    private static readonly string[] OpenStatuses = ["new", "consulting", "test_scheduled"];
    private static readonly string[] ActiveEnrollment = ["studying", "reserved"];

    /// <summary>Khóa tuổi nợ của Finance → khóa <c>AGING</c> frontend</summary>
    private static readonly (string Finance, string Key)[] AgingKeys =
        [("not_due", "not_due"), ("days1_to30", "d30"), ("days31_to60", "d60"), ("over60", "d60p")];

    /// <param name="branches">Chi nhánh trong phạm vi; null = mọi chi nhánh</param>
    public async Task<ReportDto> BuildAsync(ReportPeriod period, IReadOnlyCollection<Guid>? branches, Guid? selected, CancellationToken ct)
    {
        var from = TimeText.At(period.From, TimeOnly.MinValue);
        var leadRows = await leads.LeadsCreatedAsync(branches, from, ct);
        var campaigns = await leads.CampaignsAsync(branches, ct);
        var enrollRows = await enrollments.EnrollmentsAsync(branches, period.From, ct);
        var classes = await academic.OpenClassesAsync(branches, ct);
        var attendance = await academic.AttendanceByMonthAsync(branches, period.From, period.To, ct);
        var week = RoomUtilization.WeekOf(period.Today);
        var rooms = await academic.RoomUsageAsync(branches, week.From, week.To, ct);
        var cash = await finance.CashByMonthAsync(branches, period.From, ct);
        var booked = await finance.BookedAsync(branches, period.From, ct);
        var debts = await finance.DebtsAsync(branches, ct);
        var people = await employees.ListActiveAsync(ct);
        var branchList = (await branchDirectory.ListAsync(activeOnly: true, ct)).Where(b => branches is null || branches.Contains(b.Id)).ToList();

        string Key(DateOnly m) => ReportPeriod.KeyOf(m);
        MonthPointDto Point(DateOnly m, decimal? value) => new(Key(m), period.LabelOf(m), value);
        DateOnly MonthOf(DateOnly d) => new(d.Year, d.Month, 1);

        // ---------- Tuyển sinh (BC-03) ----------
        var won = leadRows.Count(l => l.Status == "won");
        var funnel = FunnelOrder.Select(s => new KeyCountDto(s, leadRows.Count(l => l.Status == s))).ToList();
        var bySource = leadRows.GroupBy(l => l.Source)
            .Select(g => new SourceRowDto(g.Key, g.Count(), g.Count(l => l.Status == "won"), Ratio.Percent(g.Count(l => l.Status == "won"), g.Count())))
            .OrderByDescending(s => s.Leads).ThenBy(s => s.Key, StringComparer.Ordinal).ToList();
        var sales = people.Where(e => e.Department == SalesDepartment && !e.HasQuit && (branches is null || branches.Contains(e.BranchId)));
        var bySale = sales.Select(s =>
            {
                var mine = leadRows.Where(l => l.AssigneeId == s.Id).ToList();
                var w = mine.Count(l => l.Status == "won");
                return new SaleRowDto(s.Id, s.FullName, mine.Count, w, mine.Count(l => OpenStatuses.Contains(l.Status)), Ratio.Percent(w, mine.Count));
            })
            .OrderByDescending(s => s.Won).ThenByDescending(s => s.Leads).ThenBy(s => s.Label, StringComparer.Ordinal).ToList();
        var byCampaign = CampaignCost.Rank(campaigns.Select(c => new CampaignCost(c.Id, c.Code, c.Name, c.Spent, c.Leads, c.Won)))
            .Select(c => new CampaignRowDto(c.CampaignId, c.Code, c.Name, c.Spent, c.Leads, c.Won, c.CostPerLead, c.CostPerWon)).ToList();
        var leadsByMonth = period.MonthStarts
            .Select(m => Point(m, leadRows.Count(l => MonthOf(TimeText.DateOf(l.CreatedAt)) == m))).ToList();
        var lead = new LeadReportDto(leadRows.Count, won, Ratio.Percent(won, leadRows.Count), leadRows.Count(l => OpenStatuses.Contains(l.Status)),
            funnel, bySource, bySale, byCampaign, leadsByMonth);

        // ---------- Doanh thu & công nợ (BC-04) ----------
        decimal CashIn(DateOnly month, Guid? branch = null) =>
            cash.Where(c => c.Month == month && (branch is null || c.BranchId == branch)).Sum(c => c.Collected);
        var revenue = period.MonthStarts.Select(m => Point(m, CashIn(m))).ToList();
        var thisMonth = CashIn(period.CurrentMonth);
        var prevMonth = CashIn(period.PreviousMonth);
        var aging = AgingKeys.Select(a =>
        {
            var rows = debts.Where(d => d.Aging == a.Finance).ToList();
            return new AgingRowDto(a.Key, rows.Count, rows.Sum(d => d.Remaining));
        }).ToList();
        var money = new MoneyReportDto(revenue, thisMonth, prevMonth, Ratio.Growth(thisMonth, prevMonth), booked.Sum(b => b.Amount),
            cash.Where(c => c.Month >= period.From).Sum(c => c.Collected), debts.Sum(d => d.Remaining), debts.Where(d => d.Overdue).Sum(d => d.Remaining),
            aging);

        // ---------- Đào tạo (BC-05) ----------
        var active = enrollRows.Where(e => ActiveEnrollment.Contains(e.Status)).ToList();
        var filledByClass = active.GroupBy(e => e.ClassId).ToDictionary(g => g.Key, g => g.Count());
        var studying = enrollRows.Where(e => e.Status == "studying").Select(e => e.StudentId).Distinct().Count();
        var newEnroll = period.MonthStarts.Select(m => Point(m, enrollRows.Count(e => MonthOf(e.EnrolledAt) == m))).ToList();
        var attendanceByMonth = period.MonthStarts.Select(m =>
        {
            var tally = attendance.Where(a => a.Month == m).Aggregate(AttendanceTally.Empty, (t, a) => t + new AttendanceTally(a.Attended, a.Total));
            return Point(m, tally.Rate);
        }).ToList();
        var fill = classes.Select(c =>
            {
                var n = filledByClass.GetValueOrDefault(c.Id);
                return new FillRowDto(c.Id, c.Code, c.CourseName, n, c.MaxStudents, Ratio.Percent(n, c.MaxStudents) ?? 0, c.Status);
            })
            .OrderBy(c => c.Rate).ThenBy(c => c.Label, StringComparer.Ordinal).ToList();
        var roomUse = rooms.Select(r => new RoomUseRowDto(r.RoomId, r.Name, r.BranchId, RoomUtilization.Hours(r.Minutes), RoomUtilization.Rate(r.Minutes)))
            .OrderByDescending(r => r.Rate).ThenBy(r => r.Label, StringComparer.Ordinal).ToList();
        var study = new StudyReportDto(studying, classes.Count(c => c.Status == "ongoing"), classes.Count(c => c.Status == "enrolling"), newEnroll,
            attendanceByMonth, fill, roomUse, roomUse.Count == 0 ? null : (int)Math.Round(roomUse.Average(r => r.Rate), MidpointRounding.AwayFromZero));

        // ---------- So sánh chi nhánh (BC-06) ----------
        var branchRows = BranchComparison.Compare(branchList.Select(b =>
            {
                var bl = leadRows.Where(l => l.BranchId == b.Id).ToList();
                var bc = classes.Where(c => c.BranchId == b.Id).ToList();
                var tally = attendance.Where(a => a.BranchId == b.Id).Aggregate(AttendanceTally.Empty, (t, a) => t + new AttendanceTally(a.Attended, a.Total));
                var bd = debts.Where(d => d.BranchId == b.Id).ToList();
                return new BranchFigures(b.Id, b.Name, bl.Count, bl.Count(l => l.Status == "won"),
                    enrollRows.Where(e => e.BranchId == b.Id && e.Status == "studying").Select(e => e.StudentId).Distinct().Count(),
                    bc.Count, bc.Sum(c => c.MaxStudents), bc.Sum(c => filledByClass.GetValueOrDefault(c.Id)), tally,
                    period.MonthStarts.Sum(m => CashIn(m, b.Id)), CashIn(period.CurrentMonth, b.Id), bd.Sum(d => d.Remaining),
                    bd.Where(d => d.Overdue).Sum(d => d.Remaining));
            }))
            .Select(r => new BranchRowDto(r.Figures.BranchId, r.Figures.Name, r.Figures.Leads, r.Conversion, r.Figures.Students, r.Figures.Classes, r.Fill,
                r.Attendance, r.Figures.Collected, r.Figures.CollectedThisMonth, r.Figures.Debt, r.Figures.Overdue, r.NeedsAttention))
            .ToList();

        return new ReportDto(period.Months, selected, period.MonthStarts.Select(Key).ToList(), lead, money, study, branchRows);
    }
}
