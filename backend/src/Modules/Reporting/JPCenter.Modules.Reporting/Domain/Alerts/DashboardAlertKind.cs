namespace JPCenter.Modules.Reporting.Domain.Alerts;

/// <summary>Loại cảnh báo ở khối "Cần xử lý" (BC-01)</summary>
public enum DashboardAlertKind
{
    LeadSlaOverdue,
    UnconfirmedSessions,
    MissingTeacherLogs,
    LowFillClasses,
    IntegrationError,
}
