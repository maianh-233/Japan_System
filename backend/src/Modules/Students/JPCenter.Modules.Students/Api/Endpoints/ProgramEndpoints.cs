using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abroad.ChangeAbroadCase;
using JPCenter.Modules.Students.Application.Abroad.Common;
using JPCenter.Modules.Students.Application.Abroad.CreateAbroadCase;
using JPCenter.Modules.Students.Application.Abroad.GetAbroadCases;
using JPCenter.Modules.Students.Application.Abroad.UpdateAbroadCase;
using JPCenter.Modules.Students.Application.Certificates.GetCertificates;
using JPCenter.Modules.Students.Application.Certificates.IssueCertificate;
using JPCenter.Modules.Students.Application.Certificates.ResendCertificate;
using JPCenter.Modules.Students.Application.Certificates.VerifyCertificate;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Jlpt.ChangeJlptRegistration;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Application.Jlpt.GetJlptRegistrations;
using JPCenter.Modules.Students.Application.Jlpt.GetJlptSessions;
using JPCenter.Modules.Students.Application.Jlpt.GetJlptStats;
using JPCenter.Modules.Students.Application.Jlpt.RegisterJlpt;
using JPCenter.Modules.Students.Application.Jlpt.SaveJlptSession;
using JPCenter.Modules.Students.Application.Jlpt.UpdateJlptRegistration;
using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Students.Api.Endpoints;

/// <summary>
/// Nhóm HV riêng (giai đoạn 23) — <c>/api/v1/students/…</c>: chứng nhận hoàn thành HV-15 (<c>students.certificates.*</c>, tra cứu công khai),
/// JLPT HV-16 (<c>students.jlpt.*</c>), du học / XKLĐ HV-17 (<c>students.study-abroad.*</c>).
/// </summary>
public static class ProgramEndpoints
{
    public sealed record CertificateBody(string? Email, string? Exception);

    public sealed record SessionBody(string? Label, DateOnly ExamDate, DateOnly RegClose, DateOnly ResultDate, Dictionary<string, decimal>? Fees,
        List<string>? Venues);

    public sealed record RegisterBody(Guid SessionId, List<Guid>? StudentIds, JlptLevel Level, string? Venue);

    public sealed record RegistrationBody(JlptLevel Level, string? Venue, JlptDocsDto? Docs, string? Note);

    public sealed record StepBody(string? AdmitNo, List<int>? Scores, string? Note, string? No, string? HandedTo);

    public sealed record AbroadBody(Guid StudentId, AbroadProgram Program, string? Partner, string? Destination, string? Job, string? Intake,
        JlptLevel? JlptRequired, decimal FeeTotal, Guid AssigneeId, uint? RowVersion)
    {
        public AbroadCaseInput ToInput() => new(Program, Partner, Destination, Job, Intake, JlptRequired, FeeTotal, AssigneeId);
    }

    public sealed record DocumentBody(DocumentState State);

    public sealed record AmountBody(decimal Amount);

    public sealed record TextBody(string? Text);

    public static void Map(IEndpointRouteBuilder students)
    {
        MapCertificates(students);
        MapJlpt(students);
        MapAbroad(students);
    }

    private static void MapCertificates(IEndpointRouteBuilder students)
    {
        const string page = StudentAccess.Certificates;
        students.MapGet("/certificates", async (Guid? classId, bool? eligible, ISender s, CancellationToken ct) =>
                (await s.Send(new GetCertificatesQuery(classId, eligible), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        students.MapPost("/enrollments/{id:guid}/certificate", async (Guid id, CertificateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new IssueCertificateCommand(id, b.Email, b.Exception), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Create);

        students.MapPost("/enrollments/{id:guid}/certificate/resend", async (Guid id, CertificateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ResendCertificateCommand(id, b.Email), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // HV-15 bước 4 — trang /verify/{code} (QR trên chứng nhận), không cần đăng nhập
        students.MapGet("/public/certificates/{code}", async (string code, ISender s, CancellationToken ct) =>
                (await s.Send(new VerifyCertificateQuery(code), ct)).ToHttpResult())
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Anonymous);
    }

    private static void MapJlpt(IEndpointRouteBuilder students)
    {
        const string page = StudentAccess.Jlpt;
        var sessions = students.MapGroup("/jlpt-sessions");
        sessions.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetJlptSessionsQuery(), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        sessions.MapPost("/", async (SessionBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveJlptSessionCommand(null, b.Label, b.ExamDate, b.RegClose, b.ResultDate, b.Fees, b.Venues), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Create);
        sessions.MapPut("/{id:guid}", async (Guid id, SessionBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveJlptSessionCommand(id, b.Label, b.ExamDate, b.RegClose, b.ResultDate, b.Fees, b.Venues), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        var regs = students.MapGroup("/jlpt-registrations");
        regs.MapGet("/", async (Guid? sessionId, Guid? studentId, QueryEnum<JlptRegistrationStatus>? status, ISender s, CancellationToken ct) =>
                (await s.Send(new GetJlptRegistrationsQuery(sessionId, studentId, status?.Value), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        regs.MapGet("/stats", async (Guid? sessionId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetJlptStatsQuery(sessionId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        regs.MapPost("/", async (RegisterBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new RegisterJlptCommand(b.SessionId, b.StudentIds ?? [], b.Level, b.Venue), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Create);
        regs.MapPut("/{id:guid}", async (Guid id, RegistrationBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateJlptRegistrationCommand(id, b.Level, b.Venue, b.Docs, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        foreach (var (path, step) in new[]
                 {
                     ("pay", JlptStep.Pay), ("submit", JlptStep.Submit), ("admit", JlptStep.Admit), ("result", JlptStep.Result),
                     ("absent", JlptStep.Absent), ("cancel", JlptStep.Cancel), ("cert-received", JlptStep.CertReceived),
                     ("cert-handed", JlptStep.CertHanded),
                 })
        {
            regs.MapPost($"/{{id:guid}}/{path}", async (Guid id, StepBody? b, ISender s, CancellationToken ct) =>
                    (await s.Send(new ChangeJlptRegistrationCommand(id, step, b?.AdmitNo, b?.Scores, b?.Note, b?.No, b?.HandedTo), ct)).ToHttpResult())
                .RequirePermission(page, PermissionAction.Update);
        }
    }

    private static void MapAbroad(IEndpointRouteBuilder students)
    {
        const string page = StudentAccess.Abroad;
        var g = students.MapGroup("/abroad-cases");
        g.MapGet("/", async (Guid? studentId, QueryEnum<AbroadProgram>? program, QueryEnum<AbroadStage>? stage, bool? includeCancelled, ISender s,
                    CancellationToken ct) =>
                (await s.Send(new GetAbroadCasesQuery(studentId, program?.Value, stage?.Value, includeCancelled ?? false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        g.MapPost("/", async (AbroadBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateAbroadCaseCommand(b.StudentId, b.ToInput()), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Create);
        g.MapPut("/{id:guid}", async (Guid id, AbroadBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateAbroadCaseCommand(id, b.ToInput(), b.RowVersion ?? 0), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/advance", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeAbroadCaseCommand(id, AbroadStep.Advance), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        g.MapPut("/{id:guid}/documents/{documentId:guid}", async (Guid id, Guid documentId, DocumentBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeAbroadCaseCommand(id, AbroadStep.Document, DocumentId: documentId, State: b.State), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        g.MapPut("/{id:guid}/milestones", async (Guid id, AbroadMilestones b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeAbroadCaseCommand(id, AbroadStep.Milestones, Milestones: b), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        g.MapPost("/{id:guid}/payments", async (Guid id, AmountBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeAbroadCaseCommand(id, AbroadStep.Payment, Amount: b.Amount), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        g.MapPost("/{id:guid}/notes", async (Guid id, TextBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeAbroadCaseCommand(id, AbroadStep.Note, Text: b.Text), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        g.MapPost("/{id:guid}/cancel", async (Guid id, TextBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeAbroadCaseCommand(id, AbroadStep.Cancel, Text: b.Text), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        g.MapPost("/{id:guid}/reopen", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeAbroadCaseCommand(id, AbroadStep.Reopen), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
    }
}
