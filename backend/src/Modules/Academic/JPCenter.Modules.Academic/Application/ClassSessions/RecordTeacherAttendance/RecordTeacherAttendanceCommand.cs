using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.ClassSessions.RecordTeacherAttendance;

/// <summary>
/// DT-12 bước 3 — giáo vụ chấm công hộ GV / trợ giảng: trạng thái, giờ vào / ra, ghi chú; GV vắng → người dạy thay.
/// Ghi nhật ký hỗ trợ "Chấm công hộ". Buổi đã xác nhận vẫn sửa được (có nhật ký — E3).
/// </summary>
public sealed record RecordTeacherAttendanceCommand(Guid Id, Guid TeacherId, StaffRole Role, TeacherAttendanceStatus Status, TimeOnly? CheckIn,
    TimeOnly? CheckOut, Guid? SubstituteId, string? Note) : ICommand<SessionResult>;

public sealed class RecordTeacherAttendanceValidator : AbstractValidator<RecordTeacherAttendanceCommand>
{
    public RecordTeacherAttendanceValidator()
    {
        RuleFor(x => x.TeacherId).NotEmpty().WithMessage("Chọn GV / trợ giảng");
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.SubstituteId).NotEqual(x => x.TeacherId).WithMessage("Người dạy thay phải khác GV vắng");
    }
}

public sealed class RecordTeacherAttendanceHandler(IAcademicDbContext db, SessionReader reader, AcademicAccess access, PeopleNames people,
    IClock clock, IAuditTrail audit) : ICommandHandler<RecordTeacherAttendanceCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(RecordTeacherAttendanceCommand cmd, CancellationToken ct)
    {
        if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct) is not var (session, cls)) return SessionErrors.NotFound;
        var confirmed = session.IsConfirmed;
        var r = session.RecordStaffAttendance(cmd.TeacherId, cmd.Role, CheckSource.Staff, cmd.Status, cmd.CheckIn, cmd.CheckOut, cmd.SubstituteId,
            cmd.Note, clock.UtcNow);
        if (r.IsFailure) return r.Error;

        var names = await people.OfAsync([cmd.TeacherId, cmd.SubstituteId], ct);
        var who = names.GetValueOrDefault(cmd.TeacherId, "GV");
        var text = cmd.Status switch
        {
            TeacherAttendanceStatus.Absent => cmd.SubstituteId is { } sub ? $"{who} vắng — {names.GetValueOrDefault(sub, "GV khác")} dạy thay" : $"{who} vắng",
            TeacherAttendanceStatus.Late => $"{who} đi muộn",
            _ => $"{who} có mặt",
        };
        session.AddSupport(SupportKind.TeacherCheckin, $"Chấm công hộ: {text}", access.ActorId, clock.UtcNow);
        await db.SaveChangesAsync(ct);

        if (confirmed)
            audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Sửa chấm công buổi đã xác nhận", $"{cls.Code} · buổi {session.No}", text);
        var warnings = new List<string>();
        if (cmd.Status == TeacherAttendanceStatus.Absent && cmd.SubstituteId is null)
            warnings.Add("GV vắng và chưa có người dạy thay — buổi không tính lương cho GV này; cân nhắc hủy buổi và xếp học bù");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), warnings);
    }
}
