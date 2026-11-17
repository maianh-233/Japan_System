using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Courses.ActivateCourse;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Application.Courses.CreateCourse;
using JPCenter.Modules.Academic.Application.Courses.GetCourse;
using JPCenter.Modules.Academic.Application.Courses.GetCourseOptions;
using JPCenter.Modules.Academic.Application.Courses.GetCourses;
using JPCenter.Modules.Academic.Application.Courses.LinkCurriculum;
using JPCenter.Modules.Academic.Application.Courses.PauseCourse;
using JPCenter.Modules.Academic.Application.Courses.SetDefaultCurriculum;
using JPCenter.Modules.Academic.Application.Courses.UnlinkCurriculum;
using JPCenter.Modules.Academic.Application.Courses.UpdateCourse;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>KH-01 → KH-06 — <c>/api/v1/academic/courses</c>, quyền <c>academic.courses.*</c></summary>
public static class CourseEndpoints
{
    public sealed record CourseBody(string Code, string Name, CourseCategory Category, JlptLevel LevelFrom, JlptLevel LevelTo,
        CourseFormat Format, TeacherType TeacherType, StudyMode Mode, int Sessions, int MinutesPerSession, int SessionsPerWeek, decimal Fee,
        int? MaxStudents, List<Guid> BranchIds, string? Description, Guid? CloneFromId, uint? RowVersion)
    {
        public CourseInput ToInput() => new(Code, Name, Category, LevelFrom, LevelTo, Format, TeacherType, Mode, Sessions, MinutesPerSession,
            SessionsPerWeek, Fee, MaxStudents, BranchIds ?? [], Description);
    }

    public sealed record LinkBody(Guid CurriculumId, bool IsDefault);

    public static void Map(IEndpointRouteBuilder academic)
    {
        var g = academic.MapGroup("/courses");
        const string page = CourseReader.Page;

        g.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetCoursesQuery(), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // KH-01.E5 — ô chọn khóa ở trang khác: mọi nhân viên đã đăng nhập
        g.MapGet("/options", async (Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetCourseOptionsQuery(branchId), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff);

        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetCourseQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapPost("/", async (CourseBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateCourseCommand(b.ToInput(), b.CloneFromId), ct)).ToCreatedResult(c => $"/api/v1/academic/courses/{c.Id}"))
            .RequirePermission(page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, CourseBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateCourseCommand(id, b.ToInput(), b.RowVersion ?? 0), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/activate", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new ActivateCourseCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/pause", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new PauseCourseCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/curricula", async (Guid id, LinkBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new LinkCurriculumCommand(id, b.CurriculumId, b.IsDefault), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPut("/{id:guid}/curricula/{curriculumId:guid}/default", async (Guid id, Guid curriculumId, ISender s, CancellationToken ct) =>
                (await s.Send(new SetDefaultCurriculumCommand(id, curriculumId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapDelete("/{id:guid}/curricula/{curriculumId:guid}", async (Guid id, Guid curriculumId, ISender s, CancellationToken ct) =>
                (await s.Send(new UnlinkCurriculumCommand(id, curriculumId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
    }
}
