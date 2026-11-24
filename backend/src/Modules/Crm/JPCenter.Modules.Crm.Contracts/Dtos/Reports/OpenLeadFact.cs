namespace JPCenter.Modules.Crm.Contracts.Dtos.Reports;

/// <summary>Lead đang mở (mới / tư vấn / hẹn test, không ẩn) cho Việc hôm nay + cảnh báo SLA (BC-01, BC-02).</summary>
/// <param name="Interest">Khóa quan tâm (mã frontend, vd <c>n5</c>)</param>
/// <param name="SlaMinutes">SLA liên hệ lần đầu theo quy tắc chia lead khớp / Cài đặt chung</param>
/// <param name="SlaOverdue">Đã quá SLA (CRM-01.E2)</param>
public sealed record OpenLeadFact(Guid Id, string FullName, string Phone, string? Interest, Guid BranchId, Guid? AssigneeId, string? AssigneeName,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? LastContactAt, DateTimeOffset? NextFollowUpAt, int SlaMinutes, bool SlaOverdue);
