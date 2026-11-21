using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>Kết quả kiểm SLA 1 lead (CRM-11) — job quét mỗi phút dùng; chạy lại an toàn nhờ 2 mốc trên lead (CRM-11.E3).</summary>
/// <param name="SlaMinutes">SLA áp dụng (theo quy tắc khớp, không có → mặc định Cài đặt chung)</param>
/// <param name="LateMinutes">Số phút trễ so với SLA (≥ 0 khi quá hạn)</param>
/// <param name="Escalate">Cần cảnh báo (chưa cảnh báo lần nào)</param>
/// <param name="Reassign">Cần tự chuyển Sale (quy tắc bật, quá 2×SLA, chưa tự chuyển lần nào)</param>
public sealed record SlaCheck(bool Overdue, int SlaMinutes, int LateMinutes, bool Escalate, bool Reassign)
{
    /// <param name="createdAt">Cột chung created_at của lead</param>
    public static SlaCheck Of(Lead lead, DateTimeOffset createdAt, LeadRule? rule, int defaultSlaMinutes, DateTimeOffset now)
    {
        var sla = rule?.SlaMinutes ?? defaultSlaMinutes;
        if (!lead.IsSlaOverdue(createdAt, sla, now)) return new(false, sla, 0, false, false);
        var late = (int)Math.Floor((now - createdAt).TotalMinutes) - sla;
        return new(true, sla, late, lead.SlaEscalatedAt is null,
            rule is { ReassignOnBreach: true } && lead.SlaReassignedAt is null && late >= sla);
    }
}
