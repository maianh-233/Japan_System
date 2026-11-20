namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>Cài đặt chung của trung tâm cho module khác (CD-03) — chỉ đọc, đã kiểm tra hợp lệ.</summary>
/// <param name="InvoiceResetEvery">"never" · "year" · "month"</param>
/// <param name="RefundApprovalMin">VND — hoàn tiền từ mức này cần người thứ 2 duyệt</param>
public sealed record CenterSettingsSnapshot(
    int CrmDefaultSlaMinutes,
    OfficeHours Office,
    string InvoiceCodePattern,
    string InvoiceResetEvery,
    int PayrollLockDay,
    AtRiskThresholds AtRisk,
    ParentNoticeOptions Parent,
    decimal RefundApprovalMin,
    int TrashDays);
