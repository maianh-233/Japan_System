using FluentValidation;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Realtime;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.GetSessionQr;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Academic.Application.ClassSessions.ScanAttendance;

/// <summary>
/// DT-09 bước 2 — HV quét QR (<paramref name="Payload"/> = <c>JPC-ATT|{sessionId}|{mã}</c>) hoặc nhập mã 6 ký tự (<paramref name="Code"/>).
/// Mã 6 ký tự không chứa Id buổi → tìm trong các buổi hôm nay. Có mặt / muộn (quá 10 phút); quét lần 2 giữ lần đầu (E8).
/// Đẩy realtime <c>attendance.scanned</c> cho GV đang mở màn hình QR.
/// </summary>
public sealed record ScanAttendanceCommand(string? Payload, string? Code) : ICommand<ScanResultDto>;

public sealed record ScanResultDto(Guid SessionId, Guid ClassId, string ClassCode, string ClassName, int No, DateOnly Date, string StartTime,
    AttendanceStatus Status, string? CheckInAt);

public sealed class ScanAttendanceValidator : AbstractValidator<ScanAttendanceCommand>
{
    public ScanAttendanceValidator()
    {
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Payload) || !string.IsNullOrWhiteSpace(x.Code))
            .WithName("code").WithMessage("Quét mã QR hoặc nhập mã 6 ký tự");
        RuleFor(x => x.Payload).MaximumLength(200);
        RuleFor(x => x.Code).MaximumLength(64);
    }
}

public sealed class ScanAttendanceHandler(IAcademicDbContext db, PortalOwner owner, IEnrollmentLookup enrollments, MakeupSync makeups,
    IClock clock, IIntegrationEventPublisher events, IRealtimeNotifier realtime) : ICommandHandler<ScanAttendanceCommand, ScanResultDto>
{
    public async Task<Result<ScanResultDto>> Handle(ScanAttendanceCommand cmd, CancellationToken ct)
    {
        var me = await owner.StudentAsync(null, ct);
        if (me.IsFailure) return me.Error;
        var studentId = me.Value;
        var now = clock.LocalNow;

        var (session, code) = await FindAsync(cmd, studentId, DateOnly.FromDateTime(now), ct);
        if (session is null) return SessionErrors.InvalidCode;

        var cls = await db.Classes.IgnoreQueryFilters().FirstAsync(c => c.Id == session.ClassId, ct);
        var inClass = (await enrollments.OfStudentAsync(studentId, ct))
            .Any(e => e.ClassId == cls.Id.Value && e.Status is "studying" or "reserved");
        var scanned = session.ScanQr(studentId, code, inClass, now, clock.UtcNow);
        if (scanned.IsFailure) return scanned.Error;

        await makeups.ApplyAsync(session, [studentId], ct);
        events.Publish(new Ev.AttendanceRecorded(session.Id.Value, cls.Id.Value, session.BranchId.Value, session.Date, [studentId]));
        await db.SaveChangesAsync(ct);

        var record = session.Attendance.First(a => a.StudentId == studentId);
        await realtime.ToEmployeesAsync(TeacherSessions.StaffOf(session, cls), "attendance.scanned",
            new { sessionId = session.Id.Value, studentId, status = scanned.Value, checkInAt = TimeText.Of(record.CheckInAt) }, ct);
        return new ScanResultDto(session.Id.Value, cls.Id.Value, cls.Code, cls.Name, session.No, session.Date, TimeText.Of(session.StartTime),
            scanned.Value, TimeText.Of(record.CheckInAt));
    }

    /// <summary>Buổi theo nội dung QR (có Id buổi) hoặc theo mã 6 ký tự trong các buổi hôm nay — ưu tiên buổi HV có tên</summary>
    private async Task<(ClassSession? Session, string Code)> FindAsync(ScanAttendanceCommand cmd, Guid studentId, DateOnly today, CancellationToken ct)
    {
        var payload = cmd.Payload?.Trim();
        if (!string.IsNullOrEmpty(payload) && payload.StartsWith(GetSessionQrHandler.Prefix + "|", StringComparison.Ordinal))
        {
            var parts = payload.Split('|');
            if (parts.Length != 3 || !Guid.TryParse(parts[1], out var sid)) return (null, "");
            return (await db.ClassSessions.FirstOrDefaultAsync(s => s.Id == new ClassSessionId(sid), ct), parts[2]);
        }

        var code = (string.IsNullOrWhiteSpace(cmd.Code) ? payload : cmd.Code)!.Trim();
        var todays = await db.ClassSessions.Where(s => s.Date == today && s.Status != SessionStatus.Cancelled).ToListAsync(ct);
        var matches = todays.Where(s => string.Equals(s.ShortCode, code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.AttendanceQrToken, code, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count <= 1) return (matches.FirstOrDefault(), code);

        var classIds = (await enrollments.OfStudentAsync(studentId, ct)).Select(e => e.ClassId).ToHashSet();
        return (matches.FirstOrDefault(s => classIds.Contains(s.ClassId.Value) || s.Attendance.Any(a => a.StudentId == studentId)) ?? matches[0], code);
    }
}
