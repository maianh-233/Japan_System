using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.ClassSessions.AddSupportEntry;
using JPCenter.Modules.Academic.Application.ClassSessions.CancelSession;
using JPCenter.Modules.Academic.Application.ClassSessions.CheckSession;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.ConfirmSession;
using JPCenter.Modules.Academic.Application.ClassSessions.ConfirmSessions;
using JPCenter.Modules.Academic.Application.ClassSessions.CreateSession;
using JPCenter.Modules.Academic.Application.ClassSessions.EditSession;
using JPCenter.Modules.Academic.Application.ClassSessions.GetSession;
using JPCenter.Modules.Academic.Application.ClassSessions.GetSessionQr;
using JPCenter.Modules.Academic.Application.ClassSessions.GetSessions;
using JPCenter.Modules.Academic.Application.ClassSessions.RecordAttendance;
using JPCenter.Modules.Academic.Application.ClassSessions.RecordTeacherAttendance;
using JPCenter.Modules.Academic.Application.ClassSessions.ReportIncident;
using JPCenter.Modules.Academic.Application.ClassSessions.ResolveIncident;
using JPCenter.Modules.Academic.Application.ClassSessions.UnconfirmSession;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>DT-10, 11, 13, 15, 20 — <c>/api/v1/academic/class-sessions</c>, quyền <c>academic.sessions.*</c></summary>
public static class ClassSessionEndpoints
{
    public sealed record CreateBody(Guid ClassId, SessionOrigin Origin, Guid? MakeupForId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
        List<Guid>? TeacherIds, Guid? RoomId, string? Note, Guid? LessonId);

    public sealed record EditBody(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, List<Guid>? TeacherIds, List<Guid>? AssistantIds,
        Guid? RoomId, string Reason, uint? RowVersion);

    public sealed record CheckBody(Guid ClassId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, List<Guid>? TeacherIds, Guid? RoomId,
        Guid? ExceptId);

    public sealed record ReasonBody(string Reason);

    public sealed record IncidentBody(TimeOnly At, IncidentReason Reason, string? Detail, IncidentAction Action, Guid? TakeoverId, bool? Notified,
        TimeOnly? TakeoverAt);

    public sealed record OutcomeBody(IncidentOutcome Outcome, Guid? TeacherId, TimeOnly? At);

    public sealed record SupportBody(SupportKind Kind, string Note);

    public sealed record ConfirmBody(string? Note);

    public sealed record ConfirmManyBody(List<Guid> Ids, string? Note);

    public sealed record AttendanceEntryBody(Guid StudentId, AttendanceStatus Status, TimeOnly? CheckInAt, string? Note, TimeOnly? LeftAt,
        string? LeaveReason)
    {
        public AttendanceEntry ToEntry() => new(StudentId, Status, CheckInAt, Note, LeftAt, LeaveReason);
    }

    /// <summary>Điểm danh cả danh sách (DT-09) — dùng chung cho nhân viên và cổng GV</summary>
    public sealed record AttendanceBody(List<AttendanceEntryBody> Records)
    {
        public IReadOnlyList<AttendanceEntry> Entries => (Records ?? []).Select(r => r.ToEntry()).ToList();
    }

    public sealed record TeacherAttendanceBody(Guid TeacherId, StaffRole Role, TeacherAttendanceStatus Status, TimeOnly? CheckIn, TimeOnly? CheckOut,
        Guid? SubstituteId, string? Note);

    public static void Map(IEndpointRouteBuilder academic)
    {
        var g = academic.MapGroup("/class-sessions");
        const string page = AcademicAccess.Sessions;

        g.MapGet("/", async (DateOnly? from, DateOnly? to, Guid? branchId, Guid? classId, Guid? teacherId, Guid? roomId, ISender s,
                    CancellationToken ct) =>
                (await s.Send(new GetSessionsQuery(new SessionFilter(from, to, branchId, classId, teacherId, roomId)), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetSessionQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapPost("/check", async (CheckBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CheckSessionQuery(b.ClassId, b.Date, b.StartTime, b.EndTime, b.TeacherIds, b.RoomId, b.ExceptId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // Học bù / học thêm (DT-10)
        g.MapPost("/", async (CreateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateSessionCommand(b.ClassId, b.Origin, b.MakeupForId, b.Date, b.StartTime, b.EndTime, b.TeacherIds, b.RoomId,
                    b.Note, b.LessonId), ct)).ToCreatedResult(r => $"/api/v1/academic/class-sessions/{r.Session.Id}"))
            .RequirePermission(page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, EditBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new EditSessionCommand(id, b.Date, b.StartTime, b.EndTime, b.TeacherIds, b.AssistantIds, b.RoomId, b.Reason,
                    b.RowVersion ?? 0), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/cancel", async (Guid id, ReasonBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CancelSessionCommand(id, b.Reason), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/incident", async (Guid id, IncidentBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ReportIncidentCommand(id, b.At, b.Reason, b.Detail, b.Action, b.TakeoverId, b.Notified ?? false, b.TakeoverAt), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/incident/outcome", async (Guid id, OutcomeBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ResolveIncidentCommand(id, b.Outcome, b.TeacherId, b.At), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/support", async (Guid id, SupportBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AddSupportEntryCommand(id, b.Kind, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // Điểm danh hộ, QR, chấm công hộ GV (DT-09, DT-12)
        g.MapGet("/{id:guid}/qr", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetSessionQrQuery(id, AsTeacher: false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPut("/{id:guid}/attendance", async (Guid id, AttendanceBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new RecordAttendanceCommand(id, b.Entries, AsTeacher: false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPut("/{id:guid}/teacher-attendance", async (Guid id, TeacherAttendanceBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new RecordTeacherAttendanceCommand(id, b.TeacherId, b.Role, b.Status, b.CheckIn, b.CheckOut, b.SubstituteId, b.Note), ct))
                .ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // Xác nhận buổi đã dạy (DT-15)
        g.MapPost("/confirm", async (ConfirmManyBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ConfirmSessionsCommand(b.Ids ?? [], b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Approve);

        g.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmBody? b, ISender s, CancellationToken ct) =>
                (await s.Send(new ConfirmSessionCommand(id, b?.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Approve);

        g.MapPost("/{id:guid}/unconfirm", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new UnconfirmSessionCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Approve);
    }
}
