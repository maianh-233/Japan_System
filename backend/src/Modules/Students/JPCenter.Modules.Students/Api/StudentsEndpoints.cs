using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Api.Common.Validation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.DropEnrollment;
using JPCenter.Modules.Students.Application.Enrollments.Enroll;
using JPCenter.Modules.Students.Application.Enrollments.GetEnrollments;
using JPCenter.Modules.Students.Application.Enrollments.ReserveEnrollment;
using JPCenter.Modules.Students.Application.Enrollments.ResumeEnrollment;
using JPCenter.Modules.Students.Application.Enrollments.TransferEnrollments;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Application.Students.GetStudent;
using JPCenter.Modules.Students.Application.Students.GetStudents;
using JPCenter.Modules.Students.Application.Students.HideStudents;
using JPCenter.Modules.Students.Application.Students.ImportStudents;
using JPCenter.Modules.Students.Application.Students.UnhideStudent;
using JPCenter.Modules.Students.Application.Students.UpdateStudent;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Students.Api;

/// <summary>
/// HV-01 → HV-11 — <c>/api/v1/students</c>. Hồ sơ: <c>students.profiles.*</c>. Ghi danh: đứng ở Lớp học (<c>academic.classes.*</c>)
/// hoặc Hồ sơ HV (<c>students.profiles.*</c>) — chỉ cần 1 trong 2, phạm vi chi nhánh kiểm trong handler.
/// </summary>
public static class StudentsEndpoints
{
    public sealed record StudentBody(string FullName, Gender? Gender, DateOnly? Dob, string? Phone, string? Email, string? Address, string? Note,
        Guid BranchId, uint? RowVersion)
    {
        public StudentInput ToInput() => new(FullName, Gender, Dob, Phone, Email, Address, Note, BranchId);
    }

    public sealed record ImportBody(List<StudentInput> Rows, bool? ImportDuplicates);

    public sealed record IdsBody(List<Guid> Ids);

    public sealed record EnrollBody(Guid StudentId, Guid ClassId, decimal? Amount, decimal? Discount, DateOnly? DueDate);

    public sealed record ReserveBody(DateOnly Until, string Reason);

    public sealed record ReasonBody(string Reason);

    public sealed record TransferBody(Guid TargetClassId, List<TransferItem> Items, DateOnly? DueDate, string Reason);

    public static void Map(IEndpointRouteBuilder students)
    {
        const string page = StudentAccess.Profiles;
        const string classes = StudentAccess.Classes;

        // ---------- Hồ sơ học viên ----------
        students.MapGet("/", async (bool? includeHidden, Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetStudentsQuery(includeHidden ?? false, branchId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        students.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetStudentQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        students.MapPut("/{id:guid}", async (Guid id, StudentBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateStudentCommand(id, b.ToInput(), b.RowVersion ?? 0), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // HV-04 — 1 request nhiều dòng
        students.MapPost("/import", async (ImportBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ImportStudentsCommand(b.Rows ?? [], b.ImportDuplicates ?? false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Create)
            .RequireRateLimiting(RateLimitPolicies.Import)
            .WithBodyLimit(4 * 1024 * 1024);

        students.MapPost("/hide", async (IdsBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new HideStudentsCommand(b.Ids ?? []), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        students.MapPost("/{id:guid}/unhide", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new UnhideStudentCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // ---------- Ghi danh ----------
        var e = students.MapGroup("/enrollments");

        e.MapGet("/", async (Guid? classId, Guid? studentId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetEnrollmentsQuery(classId, studentId), ct)).ToHttpResult())
            .RequireAnyPermission((page, PermissionAction.View), (classes, PermissionAction.View));

        e.MapPost("/", async (EnrollBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new EnrollCommand(b.StudentId, b.ClassId, b.Amount, b.Discount, b.DueDate), ct)).ToHttpResult())
            .RequireAnyPermission((classes, PermissionAction.Update), (page, PermissionAction.Update));

        e.MapPost("/{id:guid}/reserve", async (Guid id, ReserveBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ReserveEnrollmentCommand(id, b.Until, b.Reason), ct)).ToHttpResult())
            .RequireAnyPermission((classes, PermissionAction.Update), (page, PermissionAction.Update));

        e.MapPost("/{id:guid}/resume", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new ResumeEnrollmentCommand(id), ct)).ToHttpResult())
            .RequireAnyPermission((classes, PermissionAction.Update), (page, PermissionAction.Update));

        e.MapPost("/{id:guid}/drop", async (Guid id, ReasonBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new DropEnrollmentCommand(id, b.Reason), ct)).ToHttpResult())
            .RequireAnyPermission((classes, PermissionAction.Update), (page, PermissionAction.Update));

        // HV-09 — 1 hoặc nhiều HV sang 1 lớp đích
        e.MapPost("/transfer", async (TransferBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new TransferEnrollmentsCommand(b.TargetClassId, b.Items ?? [], b.DueDate, b.Reason), ct)).ToHttpResult())
            .RequireAnyPermission((classes, PermissionAction.Update), (page, PermissionAction.Update));

        // ---------- Cảnh báo bỏ học & chăm sóc (HV-14) ----------
        Endpoints.CareEndpoints.Map(students);
    }
}
