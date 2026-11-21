using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.Common;

/// <summary>Lead — khớp <c>Lead</c> của frontend (<c>features/crm/types.ts</c>) + tên Sale, cờ quá SLA / quá hẹn, phiên bản chống sửa đè.</summary>
public sealed record LeadDto(
    Guid Id,
    string Code,
    string FullName,
    string Phone,
    string? Email,
    LeadSource Source,
    LeadStatus Status,
    Guid BranchId,
    Guid? AssigneeId,
    string? AssigneeName,
    string? Interest,
    string? Note,
    string? LostReason,
    DateTimeOffset? NextFollowUpAt,
    DateTimeOffset? LastContactAt,
    DateTimeOffset CreatedAt,
    bool Hidden,
    DateTimeOffset? HiddenAt,
    IReadOnlyList<ContactChannel> NotifyChannels,
    Guid? CampaignId,
    DateTimeOffset? SlaEscalatedAt,
    DateTimeOffset? SlaReassignedAt,
    /// <summary>CRM-01.E2 — còn Mới, chưa ai liên hệ, quá SLA (SLA theo quy tắc khớp / mặc định)</summary>
    bool SlaOverdue,
    int SlaMinutes,
    /// <summary>CRM-01.E3</summary>
    bool FollowUpOverdue,
    int LinkedStudents,
    uint Version);

/// <summary>1 dòng lịch sử tư vấn — khớp <c>Consultation</c> của frontend (<c>system</c> = dòng hệ thống)</summary>
public sealed record ConsultationDto(Guid Id, Guid LeadId, ConsultChannel Channel, ConsultResult Result, string Content, Guid? StaffId,
    string StaffName, DateTimeOffset CreatedAt, DateTimeOffset? NextFollowUpAt, bool System);

/// <summary>Liên kết lead ↔ HV — khớp <c>LeadStudentLink</c> + thông tin HV + người liên hệ khác của cùng HV (CRM-08 bước 3)</summary>
public sealed record LeadLinkDto(Guid LeadId, Guid StudentId, string StudentCode, string StudentName, DateOnly? StudentDob,
    Relationship Relationship, DateTimeOffset CreatedAt, bool Primary, bool Billing, bool Notify, IReadOnlyList<OtherContactDto> OtherContacts);

public sealed record OtherContactDto(Guid LeadId, string Code, string FullName, Relationship Relationship, bool Primary);

/// <summary>Chi tiết lead (ngăn chi tiết): thông tin + lịch sử tư vấn (mới nhất trước) + học viên liên kết</summary>
public sealed record LeadDetailDto(LeadDto Lead, IReadOnlyList<ConsultationDto> Consultations, IReadOnlyList<LeadLinkDto> Students);

/// <summary>Cảnh báo không chặn (CRM-02.E3): trùng SĐT với lead khác</summary>
public sealed record LeadWarning(string Code, string Message, Guid? LeadId);

/// <summary>Kết quả thêm / sửa lead: lead + cảnh báo</summary>
public sealed record LeadSaved(LeadDto Lead, IReadOnlyList<LeadWarning> Warnings);
