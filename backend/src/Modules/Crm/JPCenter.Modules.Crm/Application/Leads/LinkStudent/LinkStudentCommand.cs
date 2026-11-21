using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Guardians.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.LinkStudent;

/// <summary>Kết quả liên kết: HV liên kết của lead + cảnh báo (đổi người liên hệ chính, HV chưa có người quyết định…)</summary>
public sealed record LeadLinksResult(IReadOnlyList<LeadLinkDto> Students, IReadOnlyList<LeadWarning> Warnings);

/// <summary>
/// CRM-08 — liên kết HV đã có với lead (phụ huynh / bản thân…): dòng hệ thống, mỗi HV ≤ 1 liên hệ chính (E2), đã liên kết → E1.
/// Cổng PH của lead thấy HV ngay (sự kiện <c>LeadStudentLinked</c>).
/// </summary>
/// <param name="ViaContacts">Gọi từ trang Học viên › Phụ huynh (HV-12): phạm vi theo <c>students.contacts</c>, HV phải thuộc chi nhánh được gán</param>
public sealed record LinkStudentCommand(Guid LeadId, Guid StudentId, Relationship Relationship, bool Primary, bool Billing, bool Notify,
    bool ViaContacts = false) : ICommand<LeadLinksResult>;

public sealed class LinkStudentHandler(ICrmDbContext db, LeadEditor editor, GuardianAccess guardians, LeadReader reader, GuardianLinks links,
    IEnrollmentLookup students, IClock clock, IAuditTrail audit) : ICommandHandler<LinkStudentCommand, LeadLinksResult>
{
    public static readonly Error StudentNotFound = Error.NotFound("students.student.not_found", "Không tìm thấy học viên");

    public async Task<Result<LeadLinksResult>> Handle(LinkStudentCommand cmd, CancellationToken ct)
    {
        var lead = cmd.ViaContacts ? await guardians.LoadAsync(cmd.LeadId, PermissionAction.Update, ct) : await editor.LoadAsync(cmd.LeadId, ct);
        if (lead is null) return LeadErrors.NotFound;
        var student = (await students.SummariesAsync([cmd.StudentId], ct)).FirstOrDefault();
        if (student is null) return StudentNotFound;
        if (cmd.ViaContacts && !guardians.Covers(student, PermissionAction.Update)) return GuardianAccess.StudentNotAllowed;

        if (lead.LinkStudent(student.Id, student.FullName, cmd.Relationship, cmd.Primary, cmd.Billing, cmd.Notify, clock.UtcNow)
                is { IsFailure: true } bad)
            return bad.Error;
        var warnings = new List<LeadWarning>();
        if (cmd.Primary && await links.MakePrimaryAsync(lead, student.Id, ct) is { } changed) warnings.Add(changed);
        links.Linked(lead, student.Id);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Liên kết lead ↔ học viên", $"{lead.Code} ↔ {student.Code}", student.FullName,
            branchId: lead.BranchId.Value);
        warnings.AddRange(await links.CheckStudentAsync(student.Id, ct));
        return new LeadLinksResult(await reader.LinksAsync(lead, ct), warnings);
    }
}
