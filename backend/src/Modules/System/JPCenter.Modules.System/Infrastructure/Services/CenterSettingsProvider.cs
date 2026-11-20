using JPCenter.Modules.System.Application.Settings.Common;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Settings;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Hiện thực <see cref="ICenterSettings"/> (CD-03): đọc qua <see cref="SettingsStore"/> (cache), đổi sang kiểu của Contracts.</summary>
internal sealed class CenterSettingsProvider(SettingsStore store) : ICenterSettings
{
    public async Task<CenterSettingsSnapshot> GetAsync(CancellationToken ct)
    {
        var s = await store.GetAsync(ct);
        return new CenterSettingsSnapshot(
            s.Crm.DefaultSlaMinutes,
            new OfficeHours(s.Office.From, s.Office.To, s.Office.SatTo, s.Office.LateGraceMin),
            s.Invoice.CodePattern,
            CenterSettings.ResetCode(s.Invoice.ResetEvery),
            s.Payroll.LockDay,
            new AtRiskThresholds(s.AtRisk.Absent, s.AtRisk.Streak, s.AtRisk.AttendanceRate, s.AtRisk.SubmitRate, s.AtRisk.ScorePct,
                s.AtRisk.MediumFrom, s.AtRisk.HighFrom),
            new ParentNoticeOptions(s.Parent.ArrivalNotice, s.Parent.AbsentAlertMin, s.Parent.UnderAge, s.Parent.WeeklyReport,
                s.Parent.WeeklyDay, s.Parent.WeeklyTime),
            s.Safety.RefundApprovalMin.Amount,
            s.Safety.TrashDays);
    }
}
