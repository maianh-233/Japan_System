using System.Globalization;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Students.Contracts.Dtos;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Ev = JPCenter.Modules.Crm.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Crm.Application.Leads.EnrollLead;

/// <summary>Người học (bước 1): chính người liên hệ · HV đã liên kết · người khác</summary>
public enum EnrollWho
{
    Self,
    Linked,
    Other,
}

/// <summary>Người học mới khi chọn "người khác" (con, anh chị em…)</summary>
/// <param name="Gender">"male" · "female" · "other"</param>
public sealed record EnrollPerson(string? FullName, string? Phone, DateOnly? Dob, string? Gender, Relationship Relationship);

/// <summary>Kết quả CRM-07: lead sau khi chốt + HV + ghi danh (màn "Xem hồ sơ HV / Xem hóa đơn")</summary>
public sealed record EnrollLeadResult(LeadDetailDto Lead, Guid StudentId, string StudentCode, string StudentName, bool StudentCreated,
    Guid? EnrollmentId, string? ClassCode, decimal? Amount, decimal Discount);

/// <summary>
/// CRM-07 — chốt lead: (1) người học → (2) lớp (bỏ qua được — E8) → (3) học phí. Students tạo HV + ghi danh + phát <c>StudentEnrolled</c>
/// (Finance lập hóa đơn); Crm liên kết lead ↔ HV, dòng hệ thống, lead "Đã chốt". HV &lt; 18 tuổi → lead là người quyết định (E9).
/// Gọi kèm <c>Idempotency-Key</c> (E10). ⏳ Thu tiền ngay (TC) làm cùng Tài chính giai đoạn 19.
/// <para>Hai giao dịch nối tiếp (Students rồi Crm, mỗi module 1 DbContext) — Crm lỗi sau khi Students đã lưu thì HV vẫn có, gửi lại với
/// "HV đã liên kết" / tìm HV để liên kết tay.</para>
/// </summary>
public sealed record EnrollLeadCommand(Guid LeadId, EnrollWho Who, Guid? StudentId, EnrollPerson? Person, Guid? ClassId, decimal? Amount,
    decimal Discount, DateOnly? DueDate) : ICommand<EnrollLeadResult>;

public sealed class EnrollLeadHandler(ICrmDbContext db, CrmAccess access, LeadEditor editor, LeadReader reader, GuardianLinks links,
    IClassLookup classes, IStudentEnrollmentService enrollment, IIntegrationEventPublisher events, IClock clock)
    : ICommandHandler<EnrollLeadCommand, EnrollLeadResult>
{
    public const string StudentProfiles = "students.profiles";
    public static readonly Error ClassOtherBranch = Error.Validation("crm.lead.class_other_branch", "Chỉ ghi danh vào lớp của chi nhánh lead");
    public static readonly Error CreateStudentForbidden =
        Error.Forbidden("system.permission.forbidden", "Bạn cần quyền tạo hồ sơ học viên để ghi danh người học mới");

    public async Task<Result<EnrollLeadResult>> Handle(EnrollLeadCommand cmd, CancellationToken ct)
    {
        var lead = await editor.LoadAsync(cmd.LeadId, ct);
        if (lead is null) return LeadErrors.NotFound;
        if (cmd.ClassId is { } classId && await classes.FindAsync(classId, ct) is { } cls && cls.BranchId != lead.BranchId.Value)
            return ClassOtherBranch;

        // ---- Bước 1: người học ----
        LeadEnrollmentRequest request;
        var relationship = Relationship.Self;
        switch (cmd.Who)
        {
            case EnrollWho.Linked:
                if (cmd.StudentId is not { } sid || lead.StudentLinks.All(k => k.StudentId != sid)) return LeadErrors.NotLinked;
                request = new(sid, null, cmd.ClassId, cmd.Amount, cmd.Discount, cmd.DueDate, lead.Code);
                break;
            case EnrollWho.Self:
                if (lead.StudentLinks.Any(k => k.Relationship == Relationship.Self)) return LeadErrors.SelfAlreadyLinked;
                if (!access.Can(StudentProfiles, PermissionAction.Create)) return CreateStudentForbidden;
                request = new(null, new NewStudentInfo(lead.FullName, lead.Phone, lead.Email, cmd.Person?.Dob, cmd.Person?.Gender, lead.BranchId.Value,
                    null), cmd.ClassId, cmd.Amount, cmd.Discount, cmd.DueDate, lead.Code);
                break;
            default:
                if (!access.Can(StudentProfiles, PermissionAction.Create)) return CreateStudentForbidden;
                if (cmd.Person is not { } p) return Error.Validation("students.student.name_required", "Nhập họ tên học viên");
                relationship = p.Relationship == Relationship.Self ? Relationship.Other : p.Relationship;
                request = new(null, new NewStudentInfo(p.FullName ?? "", p.Phone, null, p.Dob, p.Gender, lead.BranchId.Value, null),
                    cmd.ClassId, cmd.Amount, cmd.Discount, cmd.DueDate, lead.Code);
                break;
        }

        // ---- Bước 2–3: Students tạo HV + ghi danh (+ hóa đơn qua sự kiện) ----
        var enrolled = await enrollment.EnrollFromLeadAsync(request, ct);
        if (enrolled.IsFailure) return enrolled.Error;
        var r = enrolled.Value;
        var now = clock.UtcNow;

        // ---- Liên kết + chốt ----
        if (r.Created)
        {
            var decider = relationship == Relationship.Self || links.IsMinor(r.Dob);
            lead.LinkStudent(r.StudentId, r.StudentName, relationship, decider, decider, true, now);
            if (decider) await links.MakePrimaryAsync(lead, r.StudentId, ct);
            links.Linked(lead, r.StudentId);
        }
        var note = r.ClassCode is null
            ? $"Tạo hồ sơ học viên {r.StudentName} ({r.StudentCode}) — chưa xếp lớp"
            : $"Ghi danh {r.StudentName} ({r.StudentCode}) vào lớp {r.ClassCode} — học phí {Vnd(r.Amount ?? 0)}"
              + (r.Discount > 0 ? $", giảm {Vnd(r.Discount)}" : "");
        var wasWon = lead.Status == LeadStatus.Won;
        if (lead.MarkWon(r.StudentId, note, now) is { IsFailure: true } bad) return bad.Error;
        if (!wasWon) events.Publish(new Ev.LeadWon(lead.Id.Value, r.StudentId, lead.AssigneeId, lead.BranchId.Value, now));
        await db.SaveChangesAsync(ct);

        return new EnrollLeadResult(await reader.DetailAsync(lead, ct), r.StudentId, r.StudentCode, r.StudentName, r.Created, r.EnrollmentId,
            r.ClassCode, r.Amount, r.Discount);
    }

    private static string Vnd(decimal amount) => amount.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN")) + "đ";
}
