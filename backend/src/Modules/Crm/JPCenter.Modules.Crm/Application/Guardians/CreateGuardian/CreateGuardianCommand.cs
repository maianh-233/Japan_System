using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Guardians.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Application.Leads.LinkStudent;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Guardians.CreateGuardian;

/// <summary>Ô nhập "Thêm phụ huynh" (HV-12 bước 3) — khớp <c>GuardianFormFields</c> frontend</summary>
public sealed record GuardianInput(string? FullName, string? Phone, string? Email, Guid BranchId, List<ContactChannel>? NotifyChannels, string? Note,
    Relationship Relationship, List<Guid>? StudentIds, bool Primary);

/// <summary>
/// HV-12 bước 3 — thêm phụ huynh / người giám hộ cho HV đã có: tạo lead (nguồn Giới thiệu, "Đã chốt") + liên kết từng HV
/// (nhận thông báo; liên hệ chính = nhận hóa đơn, tắt liên hệ chính ở người khác — CRM-08.E2).
/// SĐT đã thuộc người liên hệ khác (E1) → DÙNG LẠI lead đó (liên kết thêm HV, cập nhật kênh nhận) thay vì tạo trùng.
/// Cũng dùng khi nhập HV từ Excel có cột phụ huynh (HV-04.E3).
/// </summary>
public sealed record CreateGuardianCommand(GuardianInput Input) : ICommand<GuardianSaved>;

public sealed class CreateGuardianHandler(ICrmDbContext db, GuardianAccess access, LeadCodes codes, LeadReader reader, GuardianLinks links,
    IEnrollmentLookup students, IClock clock, IAuditTrail audit) : ICommandHandler<CreateGuardianCommand, GuardianSaved>
{
    public async Task<Result<GuardianSaved>> Handle(CreateGuardianCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        if (!access.InBranch(PermissionAction.Create, input.BranchId)) return CrmAccess.BranchNotAllowed;
        var ids = (input.StudentIds ?? []).Distinct().ToList();
        if (ids.Count == 0) return LeadErrors.StudentsRequired;
        if (input.Relationship == Relationship.Self) return LeadErrors.GuardianCannotBeSelf;
        var summaries = await students.SummariesAsync(ids, ct);
        if (summaries.Count != ids.Count) return LinkStudentHandler.StudentNotFound;
        if (summaries.Any(s => !access.Covers(s, PermissionAction.Create))) return GuardianAccess.StudentNotAllowed;

        var now = clock.UtcNow;
        var phone = PhoneNumber.Normalize(input.Phone ?? "");
        var lead = phone is null ? null
            : await db.Leads.IgnoreQueryFilters().Where(l => l.Phone == phone).OrderBy(l => l.Code).FirstOrDefaultAsync(ct);
        var reused = lead is not null;
        var linked = new List<Guid>();
        if (lead is not null)
        {
            // E1 — dùng lại người liên hệ có sẵn: liên kết thêm các HV chưa liên kết
            foreach (var s in summaries.Where(s => lead.StudentLinks.All(k => k.StudentId != s.Id)))
            {
                if (lead.LinkStudent(s.Id, s.FullName, input.Relationship, input.Primary, input.Primary, notify: true, now) is { IsFailure: true } bad)
                    return bad.Error;
                linked.Add(s.Id);
            }
            if (input.NotifyChannels is { Count: > 0 } channels) lead.SetNotifyChannels(channels);
        }
        else
        {
            var info = new LeadInfo(input.FullName ?? "", input.Phone ?? "", input.Email, LeadSource.Referral, new BranchId(input.BranchId), null,
                input.Note, null, null);
            var created = Lead.CreateGuardian(Lead.CodeFor(await codes.NextNumberAsync(ct)), info,
                summaries.Select(s => (s.Id, s.FullName)).ToList(), input.Relationship, input.Primary, now);
            if (created.IsFailure) return created.Error;
            lead = created.Value;
            lead.SetNotifyChannels(input.NotifyChannels ?? []);
            db.Leads.Add(lead);
            linked.AddRange(summaries.Select(s => s.Id));
        }

        var warnings = new List<LeadWarning>();
        if (reused)
            warnings.Add(new("crm.guardian.reused", $"SĐT đã thuộc người liên hệ {lead.Code} — {lead.FullName}: đã liên kết thêm học viên vào người này",
                lead.Id.Value));
        foreach (var id in linked)
        {
            if (input.Primary && await links.MakePrimaryAsync(lead, id, ct) is { } changed) warnings.Add(changed);
            links.Linked(lead, id);
        }
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, reused ? "Liên kết thêm học viên cho phụ huynh" : "Thêm phụ huynh / người giám hộ",
            $"{lead.Code} — {lead.FullName}", string.Join(", ", summaries.Select(s => s.Code)), branchId: lead.BranchId.Value);
        foreach (var id in linked) warnings.AddRange(await links.CheckStudentAsync(id, ct));

        var meta = (await reader.MetaAsync([lead.Id], ct))[lead.Id];
        return new GuardianSaved(GuardianMapping.ToDto(lead, meta.Created, meta.Version), reused, warnings.DistinctBy(w => w.Message).ToList());
    }
}
