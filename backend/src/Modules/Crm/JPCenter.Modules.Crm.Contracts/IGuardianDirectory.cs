namespace JPCenter.Modules.Crm.Contracts;

/// <summary>
/// Phụ huynh (lead có liên kết học viên — <c>crm.lead_student_links</c>) → các con. Cổng PH chỉ thấy / gửi đơn cho con mình (PQ-11, DT-18.E5).
/// </summary>
public interface IGuardianDirectory
{
    /// <summary>Id học viên (Students.Student) liên kết với lead phụ huynh; không có → rỗng</summary>
    Task<IReadOnlyList<Guid>> ChildrenOfAsync(Guid guardianLeadId, CancellationToken ct);
}
