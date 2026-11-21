using System.Globalization;
using System.Text.RegularExpressions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads.Events;

namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>
/// Lead = người LIÊN HỆ (chính người học hoặc phụ huynh), chưa chắc đã học. Bảng: crm.leads
/// (+ consultations, lead_student_links). Lead ↔ học viên nhiều–nhiều qua LeadStudentLink.
/// Không xóa lead — chỉ ẩn; ẩn không ảnh hưởng học viên liên kết. Lịch sử tư vấn chỉ thêm (user story 05).
/// </summary>
public sealed partial class Lead : AggregateRoot<LeadId>, IBranchScoped, IAuditable, ISoftDeletable
{
    /// <summary>Lead "mở" — tính tải Sale (least_load) và còn tư vấn tiếp</summary>
    public static readonly IReadOnlyList<LeadStatus> OpenStatuses = [LeadStatus.New, LeadStatus.Consulting, LeadStatus.TestScheduled];

    private readonly List<Consultation> _consultations = [];
    private readonly List<LeadStudentLink> _studentLinks = [];

    private Lead() { }

    /// <summary>Mã hiển thị, vd LD0001</summary>
    public string Code { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    /// <summary>SĐT đã chuẩn hóa (0xxxxxxxxx)</summary>
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

    public bool IsOpen => OpenStatuses.Contains(Status);

    /// <summary>Mã lead theo số thứ tự: 1 → LD0001</summary>
    public static string CodeFor(int number) => $"LD{number:D4}";

    /// <summary>
    /// CRM-02 — thêm lead. Trạng thái ban đầu mặc định Mới; không được tạo "Đã chốt" (chỉ qua ghi danh); "Mất" cần lý do.
    /// Sale phụ trách giao sau bằng <see cref="AssignTo"/> (chọn tay hoặc chia tự động).
    /// </summary>
    public static Result<Lead> Create(string code, LeadInfo info, LeadStatus status = LeadStatus.New, string? lostReason = null)
    {
        if (status == LeadStatus.Won) return LeadErrors.WonRequiresEnrollment;
        if (status == LeadStatus.Lost && string.IsNullOrWhiteSpace(lostReason)) return LeadErrors.LostReasonRequired;
        var lead = new Lead { Id = LeadId.New(), Code = code, Status = status, LostReason = status == LeadStatus.Lost ? lostReason!.Trim() : null };
        var set = lead.Update(info);
        if (set.IsFailure) return set.Error;
        lead.Raise(new LeadCreated(lead.Id, lead.BranchId, lead.Source));
        return lead;
    }

    /// <summary>CRM-04 — sửa thông tin (lead đang ẩn vẫn sửa được, vẫn ẩn). Họ tên + SĐT bắt buộc; SĐT chuẩn hóa +84 → 0.</summary>
    public Result Update(LeadInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.FullName)) return LeadErrors.NameRequired;
        if (string.IsNullOrWhiteSpace(info.Phone)) return LeadErrors.PhoneRequired;
        var phone = PhoneNumber.Normalize(info.Phone);
        if (!PhoneNumber.IsValid(phone)) return LeadErrors.InvalidPhone;
        var email = EmailAddress.Normalize(info.Email);
        if (email is not null && !EmailAddress.IsValid(email)) return LeadErrors.InvalidEmail;

        (FullName, Phone, Email) = (Spaces().Replace(info.FullName.Trim(), " "), phone!, email);
        (Source, BranchId, CampaignId, NextFollowUpAt) = (info.Source, info.BranchId, info.CampaignId, info.NextFollowUpAt);
        (Interest, Note) = (Clean(info.Interest), Clean(info.Note));
        return Result.Success();
    }

    public void SetNotifyChannels(IEnumerable<ContactChannel> channels) => NotifyChannels = channels.Distinct().ToList();

    /// <summary>Giao / chuyển Sale (null = bỏ giao). Trả true nếu đổi người — phát <see cref="LeadAssigned"/> để báo Sale mới.</summary>
    public bool AssignTo(Guid? saleId)
    {
        if (AssigneeId == saleId) return false;
        var previous = AssigneeId;
        AssigneeId = saleId;
        if (saleId is { } id) Raise(new LeadAssigned(Id, id, previous));
        return true;
    }

    /// <summary>
    /// CRM-05 — ghi 1 lần tư vấn. Lead Mới → Đang tư vấn (dừng đồng hồ SLA); kết quả "Hẹn test" → Hẹn test.
    /// Lead đã chốt / mất giữ nguyên giai đoạn. Có hẹn gọi lại → cập nhật; không có thì hẹn cũ đã tới hạn coi như đã gọi (xóa).
    /// </summary>
    public Result AddConsultation(ConsultChannel channel, ConsultResult result, string content, Guid staffId, string staffName,
        DateTimeOffset at, DateTimeOffset? nextFollowUpAt)
    {
        if (string.IsNullOrWhiteSpace(content)) return LeadErrors.ConsultContentRequired;
        _consultations.Add(new Consultation(channel, result, content.Trim(), staffId, staffName, at, nextFollowUpAt, isSystem: false));
        LastContactAt = at;
        if (nextFollowUpAt is not null) NextFollowUpAt = nextFollowUpAt;
        else if (NextFollowUpAt <= at) NextFollowUpAt = null;
        if (Status == LeadStatus.New) Status = LeadStatus.Consulting;
        if (result == ConsultResult.TestBooked) MarkTestScheduled();
        return Result.Success();
    }

    /// <summary>Ghi dòng hệ thống vào lịch sử tư vấn (chia lead, kết quả test, ghi danh…)</summary>
    public void AddSystemNote(string content, DateTimeOffset at, ConsultResult result = ConsultResult.Interested) =>
        _consultations.Add(new Consultation(ConsultChannel.Message, result, content, null, "Hệ thống", at, null, isSystem: true));

    /// <summary>Đã hẹn test (CRM-05.E2, CRM-14 hẹn test): chỉ chuyển khi lead Mới / Đang tư vấn</summary>
    public void MarkTestScheduled()
    {
        if (Status is LeadStatus.New or LeadStatus.Consulting) Status = LeadStatus.TestScheduled;
    }

    /// <summary>
    /// CRM-13 bước 4 — ghi kết quả test vào lịch sử tư vấn (dòng hệ thống) và cập nhật khóa quan tâm theo khóa gợi ý.
    /// </summary>
    public void RecordPlacementResult(string testName, int correct, int total, decimal percent, string? level, string? course, DateTimeOffset at)
    {
        var suggestion = level is null ? "" : $" — gợi ý {level}{(course is null ? "" : $" · {course}")}";
        AddSystemNote($"Kết quả test \"{testName}\": {correct}/{total} câu ({percent.ToString("0.#", CultureInfo.InvariantCulture)}%){suggestion}", at);
        if (!string.IsNullOrWhiteSpace(course)) Interest = course.Trim();
    }

    /// <summary>Đặt hẹn gọi lại (vd khách không đến hẹn → gọi lại sau 1 ngày — CRM-15)</summary>
    public void ScheduleFollowUp(DateTimeOffset at) => NextFollowUpAt = at;

    /// <summary>
    /// CRM-04 — chuyển giai đoạn (kéo thả Kanban). "Đã chốt" chỉ qua ghi danh (E2); lead đã chốt không đổi được;
    /// "Mất" bắt buộc lý do (E1); mở lại lead mất → xóa lý do + dòng hệ thống (E3).
    /// </summary>
    public Result ChangeStatus(LeadStatus status, string? lostReason, DateTimeOffset at)
    {
        if (status == Status && status != LeadStatus.Lost) return Result.Success();
        if (Status == LeadStatus.Won) return LeadErrors.WonLocked;
        if (status == LeadStatus.Won) return LeadErrors.WonRequiresEnrollment;
        if (status == LeadStatus.Lost)
        {
            if (string.IsNullOrWhiteSpace(lostReason)) return LeadErrors.LostReasonRequired;
            (Status, LostReason) = (LeadStatus.Lost, lostReason.Trim());
            return Result.Success();
        }
        if (Status == LeadStatus.Lost) AddSystemNote("Mở lại lead", at);
        (Status, LostReason) = (status, null);
        return Result.Success();
    }

    /// <summary>
    /// CRM-08 — liên kết học viên đã có (hoặc vừa tạo ở CRM-07). 1 lead chỉ có 1 hồ sơ "Bản thân" (CRM-07.E1).
    /// Không đổi giai đoạn — chốt lead bằng <see cref="MarkWon"/>. Cờ "liên hệ chính" duy nhất mỗi HV: Application gọi
    /// <see cref="ClearPrimary"/> trên lead khác.
    /// </summary>
    public Result LinkStudent(Guid studentId, string studentName, Relationship relationship, bool isPrimary, bool billing, bool notify,
        DateTimeOffset at)
    {
        if (_studentLinks.Any(l => l.StudentId == studentId)) return LeadErrors.AlreadyLinked;
        if (relationship == Relationship.Self && _studentLinks.Any(l => l.Relationship == Relationship.Self)) return LeadErrors.SelfAlreadyLinked;
        _studentLinks.Add(new LeadStudentLink(studentId, relationship, isPrimary, billing, notify, at));
        AddSystemNote($"Liên kết học viên {studentName} ({RelationshipLabel(relationship)})", at);
        Raise(new LeadStudentLinked(Id, studentId));
        return Result.Success();
    }

    /// <summary>CRM-08 — bật / tắt cờ liên hệ chính / nhận hóa đơn / nhận thông báo</summary>
    public Result SetLinkFlags(Guid studentId, Relationship relationship, bool isPrimary, bool billing, bool notify)
    {
        var link = _studentLinks.FirstOrDefault(l => l.StudentId == studentId);
        if (link is null) return LeadErrors.NotLinked;
        if (relationship == Relationship.Self && link.Relationship != Relationship.Self
            && _studentLinks.Any(l => l.Relationship == Relationship.Self)) return LeadErrors.SelfAlreadyLinked;
        link.Set(relationship, isPrimary, billing, notify);
        return Result.Success();
    }

    /// <summary>CRM-08.E2 — HV đã có người liên hệ chính khác → tắt cờ ở liên kết cũ. Trả true nếu có tắt.</summary>
    public bool ClearPrimary(Guid studentId)
    {
        var link = _studentLinks.FirstOrDefault(l => l.StudentId == studentId && l.IsPrimary);
        if (link is null) return false;
        link.Set(link.Relationship, false, link.Billing, link.Notify);
        return true;
    }

    /// <summary>CRM-08 — gỡ liên kết. Hồ sơ HV, lớp học, học phí giữ nguyên; PH mất quyền xem HV đó (event).</summary>
    public Result UnlinkStudent(Guid studentId, string studentName, DateTimeOffset at)
    {
        if (_studentLinks.RemoveAll(l => l.StudentId == studentId) == 0) return LeadErrors.NotLinked;
        AddSystemNote($"Gỡ liên kết học viên {studentName}", at);
        Raise(new LeadStudentUnlinked(Id, studentId));
        return Result.Success();
    }

    /// <summary>
    /// CRM-07 — chốt lead sau khi tạo / chọn học viên (+ ghi danh, hóa đơn). HV phải đã liên kết.
    /// Lead đã chốt (vd ghi danh thêm 1 bé nữa) chỉ thêm dòng hệ thống. Lead mất → chốt được (khách quay lại).
    /// </summary>
    public Result MarkWon(Guid studentId, string note, DateTimeOffset at)
    {
        if (_studentLinks.All(l => l.StudentId != studentId)) return LeadErrors.NotLinked;
        AddSystemNote(note, at, ConsultResult.Enrolled);
        if (Status == LeadStatus.Won) return Result.Success();
        (Status, LostReason) = (LeadStatus.Won, null);
        Raise(new LeadWon(Id, studentId));
        return Result.Success();
    }

    public Result Hide(DateTimeOffset at)
    {
        if (Hidden) return LeadErrors.AlreadyHidden;
        (Hidden, HiddenAt) = (true, at);
        return Result.Success();
    }

    public void Unhide() => (Hidden, HiddenAt) = (false, null);

    /// <summary>
    /// CRM-01.E2 — quá SLA: lead không ẩn, còn Mới, chưa ai liên hệ, đã quá <paramref name="slaMinutes"/> phút từ lúc tạo.
    /// <paramref name="createdAt"/> là cột chung created_at (không phải thuộc tính domain).
    /// </summary>
    public bool IsSlaOverdue(DateTimeOffset createdAt, int slaMinutes, DateTimeOffset now) =>
        !Hidden && Status == LeadStatus.New && LastContactAt is null && (now - createdAt).TotalMinutes >= slaMinutes;

    /// <summary>CRM-01.E3 — hẹn gọi lại đã qua mà lead chưa chốt / mất</summary>
    public bool IsFollowUpOverdue(DateTimeOffset now) =>
        NextFollowUpAt < now && Status is not (LeadStatus.Won or LeadStatus.Lost);

    /// <summary>CRM-11 — đánh dấu đã cảnh báo quá SLA. Trả false nếu đã cảnh báo trước đó (mỗi lead 1 lần).</summary>
    public bool MarkSlaEscalated(DateTimeOffset at)
    {
        if (SlaEscalatedAt is not null) return false;
        SlaEscalatedAt = at;
        return true;
    }

    /// <summary>CRM-11 — tự chuyển Sale do quá 2×SLA (mỗi lead 1 lần)</summary>
    public Result ReassignForSla(Guid saleId, DateTimeOffset at)
    {
        if (SlaReassignedAt is not null) return LeadErrors.SlaAlreadyReassigned;
        AssignTo(saleId);
        SlaReassignedAt = at;
        return Result.Success();
    }

    public static string RelationshipLabel(Relationship r) => r switch
    {
        Relationship.Self => "bản thân",
        Relationship.Child => "con",
        Relationship.Sibling => "anh chị em",
        Relationship.Spouse => "vợ / chồng",
        _ => "khác",
    };

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
