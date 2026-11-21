using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Guardians.Common;

/// <summary>1 liên kết của người liên hệ — khớp <c>LeadStudentLink</c> frontend</summary>
public sealed record GuardianLinkDto(Guid StudentId, Relationship Relationship, bool Primary, bool Billing, bool Notify, DateTimeOffset CreatedAt);

/// <summary>Phụ huynh / người giám hộ (HV-12) = lead có liên kết HV — thông tin liên hệ + mọi liên kết (kể cả "Bản thân")</summary>
public sealed record GuardianDto(Guid Id, string Code, string FullName, string Phone, string? Email, Guid BranchId, LeadSource Source,
    LeadStatus Status, Guid? AssigneeId, string? Note, IReadOnlyList<ContactChannel> NotifyChannels, bool Hidden, DateTimeOffset CreatedAt,
    IReadOnlyList<GuardianLinkDto> Students, uint Version);

/// <summary>Kết quả thêm / sửa: người liên hệ + đã dùng lại lead có sẵn cùng SĐT (HV-12.E1) + cảnh báo</summary>
public sealed record GuardianSaved(GuardianDto Guardian, bool Reused, IReadOnlyList<LeadWarning> Warnings);

public static class GuardianMapping
{
    public static GuardianDto ToDto(Lead l, DateTimeOffset createdAt, uint version) =>
        new(l.Id.Value, l.Code, l.FullName, l.Phone, l.Email, l.BranchId.Value, l.Source, l.Status, l.AssigneeId, l.Note, l.NotifyChannels, l.Hidden,
            createdAt, l.StudentLinks.OrderBy(k => k.CreatedAt)
                .Select(k => new GuardianLinkDto(k.StudentId, k.Relationship, k.IsPrimary, k.Billing, k.Notify, k.CreatedAt)).ToList(),
            version);
}
