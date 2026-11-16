using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads.Events;

namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>
/// Lead = người LIÊN HỆ (chính người học hoặc phụ huynh), chưa chắc đã học. Bảng: crm.leads
/// (+ consultations, lead_student_links). Lead ↔ học viên nhiều–nhiều qua LeadStudentLink.
/// Ẩn lead chỉ bật cờ — học viên liên kết không bị ảnh hưởng.
/// </summary>
public sealed class Lead : AggregateRoot<LeadId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private readonly List<Consultation> _consultations = [];
    private readonly List<LeadStudentLink> _studentLinks = [];

    private Lead() { }

    /// <summary>Mã hiển thị, vd LD0001</summary>
    public string Code { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string Phone { get; private set; } = null!;
    public string? Email { get; private set; }
    public LeadSource Source { get; private set; }
    public LeadStatus Status { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Sale phụ trách (Hr.Employee)</summary>
    public Guid? AssigneeId { get; private set; }
    /// <summary>Khóa học quan tâm (tên / mã khóa)</summary>
    public string? Interest { get; private set; }
    public string? Note { get; private set; }
    public string? LostReason { get; private set; }
    public DateTimeOffset? NextFollowUpAt { get; private set; }
    public DateTimeOffset? LastContactAt { get; private set; }
    public bool Hidden { get; private set; }
    public DateTimeOffset? HiddenAt { get; private set; }
    /// <summary>Kênh nhận thông báo khi đóng vai phụ huynh</summary>
    public List<ContactChannel> NotifyChannels { get; private set; } = [];
    public CampaignId? CampaignId { get; private set; }
    /// <summary>Đã gửi cảnh báo quá SLA (mỗi lead 1 lần)</summary>
    public DateTimeOffset? SlaEscalatedAt { get; private set; }
    /// <summary>Đã tự chuyển Sale do quá 2×SLA (mỗi lead 1 lần)</summary>
    public DateTimeOffset? SlaReassignedAt { get; private set; }
    public IReadOnlyList<Consultation> Consultations => _consultations;
    public IReadOnlyList<LeadStudentLink> StudentLinks => _studentLinks;

    public static Result<Lead> Create(string code, string fullName, string phone, string? email, LeadSource source,
        BranchId branchId, string? interest, string? note, CampaignId? campaignId)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return LeadErrors.NameRequired;
        if (string.IsNullOrWhiteSpace(phone)) return LeadErrors.PhoneRequired;
        var lead = new Lead
        {
            Id = LeadId.New(), Code = code, FullName = fullName.Trim(), Phone = phone.Trim(), Email = email, Source = source,
            BranchId = branchId, Interest = interest, Note = note, CampaignId = campaignId, Status = LeadStatus.New,
        };
        lead.Raise(new LeadCreated(lead.Id, branchId, source));
        return lead;
    }

    public void AssignTo(Guid? saleId) => AssigneeId = saleId;

    public Result AddConsultation(ConsultChannel channel, ConsultResult result, string content, Guid staffId, string staffName,
        DateTimeOffset at, DateTimeOffset? nextFollowUpAt)
    {
        if (string.IsNullOrWhiteSpace(content)) return LeadErrors.ConsultContentRequired;
        _consultations.Add(new Consultation(channel, result, content, staffId, staffName, at, nextFollowUpAt, isSystem: false));
        LastContactAt = at;
        NextFollowUpAt = nextFollowUpAt;
        if (Status == LeadStatus.New) Status = LeadStatus.Consulting;
        if (result == ConsultResult.TestBooked && Status == LeadStatus.Consulting) Status = LeadStatus.TestScheduled;
        return Result.Success();
    }

    /// <summary>Ghi dòng hệ thống vào lịch sử tư vấn (vd "Tạo hồ sơ học viên", kết quả test)</summary>
    public void AddSystemNote(string content, DateTimeOffset at) =>
        _consultations.Add(new Consultation(ConsultChannel.Message, ConsultResult.Interested, content, null, "Hệ thống", at, null, isSystem: true));

    public Result ChangeStatus(LeadStatus status, string? lostReason)
    {
        if (status == LeadStatus.Lost && string.IsNullOrWhiteSpace(lostReason)) return LeadErrors.LostReasonRequired;
        Status = status;
        LostReason = status == LeadStatus.Lost ? lostReason : null;
        return Result.Success();
    }

    /// <summary>Liên kết học viên (sau khi Students tạo hồ sơ). Lead tự chuyển "Đã chốt".</summary>
    public Result LinkStudent(Guid studentId, Relationship relationship, bool primary, bool billing, bool notify, DateTimeOffset at)
    {
        if (_studentLinks.Any(l => l.StudentId == studentId)) return LeadErrors.AlreadyLinked;
        _studentLinks.Add(new LeadStudentLink(studentId, relationship, primary, billing, notify, at));
        if (Status != LeadStatus.Won)
        {
            Status = LeadStatus.Won;
            Raise(new LeadWon(Id, studentId));
        }
        return Result.Success();
    }

    public void UnlinkStudent(Guid studentId) => _studentLinks.RemoveAll(l => l.StudentId == studentId);

    public Result Hide(DateTimeOffset at)
    {
        if (Hidden) return LeadErrors.AlreadyHidden;
        (Hidden, HiddenAt) = (true, at);
        return Result.Success();
    }

    public void Unhide() => (Hidden, HiddenAt) = (false, null);

    public void MarkSlaEscalated(DateTimeOffset at) => SlaEscalatedAt ??= at;

    public void ReassignForSla(Guid saleId, DateTimeOffset at)
    {
        AssigneeId = saleId;
        SlaReassignedAt ??= at;
    }
}
