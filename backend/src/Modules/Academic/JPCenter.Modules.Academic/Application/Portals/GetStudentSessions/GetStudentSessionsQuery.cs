using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Portals.GetStudentSessions;

/// <summary>
/// DT-20 — cổng HV / PH: thời khóa biểu của HV (mọi lớp từng ghi danh + buổi đến học bù ở lớp khác) trong khoảng ngày (≤ 3 tháng).
/// PH không truyền <paramref name="StudentId"/> → lịch của tất cả các con (E2). Chỉ trả điểm danh của chính HV, không có HV khác / học phí.
/// </summary>
public sealed record GetStudentSessionsQuery(Guid? StudentId, DateOnly From, DateOnly To) : IQuery<IReadOnlyList<StudentSessionDto>>;

public sealed record MyAttendanceDto(AttendanceStatus Status, string? CheckInAt, string? Note, bool MadeUp);

/// <param name="Guest">HV đến học bù buổi này (lớp khác)</param>
public sealed record StudentSessionDto(
    Guid Id,
    Guid StudentId,
    Guid ClassId,
    string ClassCode,
    string ClassName,
    int No,
    DateOnly Date,
    string StartTime,
    string EndTime,
    SessionKind Kind,
    SessionStatus Status,
    SessionOrigin Origin,
    IReadOnlyList<Guid> TeacherIds,
    string? Room,
    bool Online,
    string? MeetingUrl,
    bool Guest,
    MyAttendanceDto? Attendance);

public sealed class GetStudentSessionsValidator : AbstractValidator<GetStudentSessionsQuery>
{
    public GetStudentSessionsValidator() =>
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Ngày kết thúc phải sau ngày bắt đầu")
            .Must((q, to) => to.DayNumber - q.From.DayNumber <= 93).WithMessage("Chỉ xem tối đa 3 tháng");
}

public sealed class GetStudentSessionsHandler(IAcademicDbContext db, PortalOwner owner, IEnrollmentLookup enrollments)
    : IQueryHandler<GetStudentSessionsQuery, IReadOnlyList<StudentSessionDto>>
{
    public async Task<Result<IReadOnlyList<StudentSessionDto>>> Handle(GetStudentSessionsQuery q, CancellationToken ct)
    {
        IReadOnlyList<Guid> students;
        if (q.StudentId is { } one)
        {
            var s = await owner.StudentAsync(one, ct);
            if (s.IsFailure) return s.Error;
            students = [s.Value];
        }
        else students = await owner.StudentsAsync(ct);

        var result = new List<StudentSessionDto>();
        foreach (var studentId in students)
        {
            var classIds = (await enrollments.OfStudentAsync(studentId, ct)).Select(e => new ClassId(e.ClassId)).Distinct().ToList();
            var sessions = await db.ClassSessions.AsNoTracking()
                .Where(s => s.Date >= q.From && s.Date <= q.To
                    && (classIds.Contains(s.ClassId) || s.Attendance.Any(a => a.StudentId == studentId && a.MakeupForSessionId != null)))
                .ToListAsync(ct);
            var ids = sessions.Select(s => s.ClassId).Distinct().ToList();
            var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
            var rooms = await db.Rooms.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(r => r.Id, r => r.Name, ct);

            result.AddRange(sessions.Select(s =>
            {
                var c = classes[s.ClassId];
                var me = s.Attendance.FirstOrDefault(a => a.StudentId == studentId);
                return new StudentSessionDto(s.Id.Value, studentId, c.Id.Value, c.Code, c.Name, s.No, s.Date, TimeText.Of(s.StartTime),
                    TimeText.Of(s.EndTime), s.Kind, s.Status, s.Origin, SessionReader.Teachers(s, c),
                    (s.RoomId ?? c.RoomId) is { } rid ? rooms.GetValueOrDefault(rid) : null, c.Online, c.Online ? c.MeetingUrl : null,
                    me?.IsGuest ?? false,
                    me is null ? null : new MyAttendanceDto(me.Status, TimeText.Of(me.CheckInAt), me.Note, me.MadeUpSessionId is not null));
            }));
        }
        return result.OrderBy(r => r.Date).ThenBy(r => r.StartTime, StringComparer.Ordinal).ToList();
    }
}
