namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>
/// Liên kết lead ↔ học viên (phụ huynh – con, bản thân...). Bảng: crm.lead_student_links, khóa (lead_id, student_id).
/// Mỗi học viên tối đa 1 người liên hệ chính (Application tắt cờ ở lead khác — Lead.ClearPrimary).
/// </summary>
public sealed class LeadStudentLink
{
    private LeadStudentLink() { }

    internal LeadStudentLink(Guid studentId, Relationship relationship, bool isPrimary, bool billing, bool notify, DateTimeOffset createdAt) =>
        (StudentId, Relationship, IsPrimary, Billing, Notify, CreatedAt) = (studentId, relationship, isPrimary, billing, notify, createdAt);

    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    public Relationship Relationship { get; private set; }
    /// <summary>Người liên hệ CHÍNH — người quyết định với học viên nhỏ tuổi</summary>
    public bool IsPrimary { get; private set; }
    /// <summary>Nhận hóa đơn & nhắc học phí</summary>
    public bool Billing { get; private set; }
    /// <summary>Nhận thông báo điểm danh / kết quả học tập</summary>
    public bool Notify { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal void Set(Relationship relationship, bool isPrimary, bool billing, bool notify) =>
        (Relationship, IsPrimary, Billing, Notify) = (relationship, isPrimary, billing, notify);
}
