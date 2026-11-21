namespace JPCenter.Modules.Crm.Contracts;

/// <summary>
/// Phụ huynh (lead có liên kết học viên — <c>crm.lead_student_links</c>) → các con. Cổng PH chỉ thấy / gửi đơn cho con mình (PQ-11, DT-18.E5).
/// </summary>
public interface IGuardianDirectory
{
    /// <summary>Id học viên (Students.Student) liên kết với lead phụ huynh; không có → rỗng</summary>
    Task<IReadOnlyList<Guid>> ChildrenOfAsync(Guid guardianLeadId, CancellationToken ct);

    /// <summary>
    /// Người liên hệ CHÍNH (người quyết định) của các HV — Sale tư vấn lên lớp gom các con của cùng 1 PH (CRM-19 bước 2).
    /// HV không có liên hệ chính → không có trong kết quả.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, GuardianContact>> DecidersOfAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken ct);
}

/// <param name="Relationship">"self" · "child" · "sibling" · "spouse" · "other"</param>
public sealed record GuardianContact(Guid LeadId, string FullName, string Phone, string Relationship);
