using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Reporting.Domain.Periods;

/// <summary>Lỗi báo cáo (user story 17)</summary>
public static class ReportingErrors
{
    /// <summary>BC-03.E3 — kỳ báo cáo chỉ 3 / 6 / 12 tháng</summary>
    public static readonly Error InvalidPeriod = Error.Validation("reporting.period.invalid", "Kỳ báo cáo chỉ được chọn 3, 6 hoặc 12 tháng");
}
