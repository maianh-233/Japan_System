using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Classes.CancelClass;
using JPCenter.Modules.Academic.Application.Classes.CheckClassSchedule;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Classes.CreateClass;
using JPCenter.Modules.Academic.Application.Classes.FinishClass;
using JPCenter.Modules.Academic.Application.Classes.GetClass;
using JPCenter.Modules.Academic.Application.Classes.GetClasses;
using JPCenter.Modules.Academic.Application.Classes.PauseClass;
using JPCenter.Modules.Academic.Application.Classes.ResumeClass;
using JPCenter.Modules.Academic.Application.Classes.StartClass;
using JPCenter.Modules.Academic.Application.Classes.UpdateClass;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.GenerateSchedule;
using JPCenter.Modules.Academic.Application.ClassSessions.GetClassSessions;
using JPCenter.Modules.Academic.Application.ClassSessions.PreviewSchedule;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>DT-01 → DT-08 — <c>/api/v1/academic/classes</c>, quyền <c>academic.classes.*</c> (sinh lịch: <c>academic.sessions.create</c>)</summary>
public static class ClassEndpoints
{
    public sealed record ClassBody(Guid CourseId, Guid? CurriculumId, string Code, string Name, Guid BranchId, List<Guid> TeacherIds,
        List<Guid>? AssistantIds, Guid? RoomId, bool Online, string? MeetingUrl, DateOnly PlannedStartDate, DateOnly? PlannedEndDate,
        List<int> Weekdays, TimeOnly StartTime, TimeOnly EndTime, decimal TotalHours, int MinStudents, int MaxStudents, decimal Fee,
        Guid? PreviousClassId, string? Note, bool? GenerateSessions, bool? ApplyToUpcoming, uint? RowVersion, Guid? ExceptId)
    {
        public ClassInput ToInput() => new(CourseId, CurriculumId, Code, Name, BranchId, TeacherIds ?? [], AssistantIds, RoomId, Online, MeetingUrl,
            PlannedStartDate, PlannedEndDate, Weekdays ?? [], StartTime, EndTime, TotalHours, MinStudents, MaxStudents, Fee, PreviousClassId, Note);
    }

    public sealed record StartBody(DateOnly ActualStartDate, bool? Reschedule);

    public sealed record FinishBody(DateOnly ActualEndDate);

    public sealed record ReasonBody(string Reason);

    public sealed record CancelBody(string Reason, string ConfirmCode);

    public sealed record ScheduleBody(DateOnly? From, List<int>? Weekdays, TimeOnly? StartTime, TimeOnly? EndTime, int? Count, bool? SkipHolidays)
    {
        public ScheduleOptions ToOptions() => new(From, Weekdays, StartTime, EndTime, Count, SkipHolidays ?? true);
    }

    public static void Map(IEndpointRouteBuilder academic)
    {
        var g = academic.MapGroup("/classes");
        const string page = AcademicAccess.Classes;

        g.MapGet("/", async (Guid? branchId, Guid? courseId, Guid? teacherId, ClassStatus? status, ISender s, CancellationToken ct) =>
                (await s.Send(new GetClassesQuery(new ClassFilter(branchId, courseId, teacherId, status)), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetClassQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // Xem trước form: số buổi, ngày kết thúc dự kiến, lớp trùng GV / phòng (DT-02.E8)
        g.MapPost("/check", async (ClassBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CheckClassScheduleQuery(b.ToInput(), b.ExceptId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapPost("/", async (ClassBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateClassCommand(b.ToInput(), b.GenerateSessions ?? true), ct))
                .ToCreatedResult(r => $"/api/v1/academic/classes/{r.Class.Id}"))
            .RequirePermission(page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, ClassBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateClassCommand(id, b.ToInput(), b.ApplyToUpcoming ?? true, b.RowVersion ?? 0), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/start", async (Guid id, StartBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new StartClassCommand(id, b.ActualStartDate, b.Reschedule ?? false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/pause", async (Guid id, ReasonBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new PauseClassCommand(id, b.Reason), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/resume", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new ResumeClassCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/finish", async (Guid id, FinishBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new FinishClassCommand(id, b.ActualEndDate), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/cancel", async (Guid id, CancelBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CancelClassCommand(id, b.Reason, b.ConfirmCode), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // Buổi học của lớp + sinh lịch tự động (DT-08)
        g.MapGet("/{id:guid}/sessions", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetClassSessionsQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapPost("/{id:guid}/sessions/preview", async (Guid id, ScheduleBody? b, ISender s, CancellationToken ct) =>
                (await s.Send(new PreviewScheduleQuery(id, (b ?? new ScheduleBody(null, null, null, null, null, null)).ToOptions()), ct)).ToHttpResult())
            .RequirePermission(AcademicAccess.Sessions, PermissionAction.Create);

        g.MapPost("/{id:guid}/sessions/generate", async (Guid id, ScheduleBody? b, ISender s, CancellationToken ct) =>
                (await s.Send(new GenerateScheduleCommand(id, (b ?? new ScheduleBody(null, null, null, null, null, null)).ToOptions()), ct)).ToHttpResult())
            .RequirePermission(AcademicAccess.Sessions, PermissionAction.Create);
    }
}
