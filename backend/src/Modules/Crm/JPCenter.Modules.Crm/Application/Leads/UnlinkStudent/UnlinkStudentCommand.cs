using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Application.Leads.LinkStudent;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.UnlinkStudent;

/// <summary>
/// CRM-08 bước 4 — gỡ liên kết: hồ sơ HV, lớp, học phí giữ nguyên; PH mất quyền xem HV đó ngay (E5); cảnh báo HV nhỏ tuổi
/// không còn người quyết định (E3) / không ai nhận hóa đơn (E4) nhưng vẫn cho gỡ.
/// </summary>
public sealed record UnlinkStudentCommand(Guid LeadId, Guid StudentId) : ICommand<LeadLinksResult>;

public sealed class UnlinkStudentHandler(ICrmDbContext db, LeadEditor editor, LeadReader reader, GuardianLinks links, IEnrollmentLookup students,
    IClock clock, IAuditTrail audit) : ICommandHandler<UnlinkStudentCommand, LeadLinksResult>
{
    public async Task<Result<LeadLinksResult>> Handle(UnlinkStudentCommand cmd, CancellationToken ct)
    {
        var lead = await editor.LoadAsync(cmd.LeadId, ct);
        if (lead is null) return LeadErrors.NotFound;
        var name = (await students.NamesAsync([cmd.StudentId], ct)).GetValueOrDefault(cmd.StudentId) ?? "học viên";
        if (lead.UnlinkStudent(cmd.StudentId, name, clock.UtcNow) is { IsFailure: true } bad) return bad.Error;
        links.Unlinked(lead, cmd.StudentId);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Gỡ liên kết lead ↔ học viên", $"{lead.Code} ↔ {name}", branchId: lead.BranchId.Value);
        return new LeadLinksResult(await reader.LinksAsync(lead, ct), await links.CheckStudentAsync(cmd.StudentId, ct));
    }
}
