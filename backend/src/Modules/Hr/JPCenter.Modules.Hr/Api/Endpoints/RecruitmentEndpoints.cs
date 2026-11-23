using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Api.Common.Validation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Recruitment.Candidates;
using JPCenter.Modules.Hr.Application.Recruitment.Candidates.AddCandidateNote;
using JPCenter.Modules.Hr.Application.Recruitment.Candidates.CandidateCv;
using JPCenter.Modules.Hr.Application.Recruitment.Candidates.CloseCandidate;
using JPCenter.Modules.Hr.Application.Recruitment.Candidates.GetCandidates;
using JPCenter.Modules.Hr.Application.Recruitment.Candidates.MoveCandidate;
using JPCenter.Modules.Hr.Application.Recruitment.Candidates.SaveCandidate;
using JPCenter.Modules.Hr.Application.Recruitment.Candidates.ScheduleInterview;
using JPCenter.Modules.Hr.Application.Recruitment.Candidates.SendOffer;
using JPCenter.Modules.Hr.Application.Recruitment.Jobs;
using JPCenter.Modules.Hr.Application.Recruitment.Jobs.ChangeJobStatus;
using JPCenter.Modules.Hr.Application.Recruitment.Jobs.DeleteJob;
using JPCenter.Modules.Hr.Application.Recruitment.Jobs.GetJobs;
using JPCenter.Modules.Hr.Application.Recruitment.Jobs.SaveJob;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Hr.Api.Endpoints;

/// <summary>TD-01 → TD-04 — tin tuyển <c>/hr/jobs</c>, ứng viên <c>/hr/candidates</c>; quyền <c>hr.recruitment.*</c>, phạm vi theo chi nhánh của tin.</summary>
public static class RecruitmentEndpoints
{
    public sealed record CandidateBody(Guid? JobId, string FullName, string Phone, string Email, string Source, string? Japanese, decimal? ExpectedSalary);

    public sealed record StageBody(CandidateStage Stage);

    public sealed record InterviewBody(DateTimeOffset At, Guid InterviewerId, InterviewMode Mode, decimal? Score, string? Note);

    public sealed record OfferBody(decimal Salary, DateOnly StartDate);

    public sealed record ReasonBody(string Reason);

    public sealed record NoteBody(string Text);

    public const long CvLimit = 11 * 1024 * 1024;

    private static readonly Error FileRequired = Error.Validation("file.required", "Chọn tệp CV");

    public static void Map(IEndpointRouteBuilder hr)
    {
        const string page = HrScope.Recruitment;
        var jobs = hr.MapGroup("/jobs");
        jobs.MapGet("/", async (Guid? branchId, QueryEnum<JobStatus>? status, ISender s, CancellationToken ct) =>
                (await s.Send(new GetJobsQuery(branchId, status?.Value), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        jobs.MapPost("/", async (JobInput b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveJobCommand(null, b), ct)).ToCreatedResult(j => $"/api/v1/hr/jobs/{j.Id}"))
            .RequirePermission(page, PermissionAction.Create);
        jobs.MapPut("/{id:guid}", async (Guid id, JobInput b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveJobCommand(id, b), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        foreach (var (path, action) in new[] { ("pause", JobAction.Pause), ("reopen", JobAction.Reopen), ("close", JobAction.Close) })
            jobs.MapPost($"/{{id:guid}}/{path}", async (Guid id, ISender s, CancellationToken ct) =>
                    (await s.Send(new ChangeJobStatusCommand(id, action), ct)).ToHttpResult())
                .RequirePermission(page, PermissionAction.Update);
        jobs.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new DeleteJobCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Delete);
        jobs.MapGet("/{id:guid}/candidates", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetCandidatesQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        var c = hr.MapGroup("/candidates");
        c.MapGet("/", async (Guid? jobId, ISender s, CancellationToken ct) => (await s.Send(new GetCandidatesQuery(jobId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        c.MapPost("/", async (CandidateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveCandidateCommand(null, b.JobId, Input(b)), ct)).ToCreatedResult(r => $"/api/v1/hr/candidates/{r.Candidate.Id}"))
            .RequirePermission(page, PermissionAction.Update);
        c.MapPut("/{id:guid}", async (Guid id, CandidateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveCandidateCommand(id, null, Input(b)), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        c.MapPost("/{id:guid}/stage", async (Guid id, StageBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new MoveCandidateCommand(id, b.Stage), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        c.MapPost("/{id:guid}/interview", async (Guid id, InterviewBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ScheduleInterviewCommand(id, b.At, b.InterviewerId, b.Mode, b.Score, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        c.MapPost("/{id:guid}/offer", async (Guid id, OfferBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SendOfferCommand(id, b.Salary, b.StartDate), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        c.MapPost("/{id:guid}/offer-declined", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new CloseCandidateCommand(id, CandidateOutcome.OfferDeclined, null), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        c.MapPost("/{id:guid}/hire", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new CloseCandidateCommand(id, CandidateOutcome.Hire, null), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        c.MapPost("/{id:guid}/reject", async (Guid id, ReasonBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CloseCandidateCommand(id, CandidateOutcome.Reject, b.Reason), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        c.MapPost("/{id:guid}/notes", async (Guid id, NoteBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AddCandidateNoteCommand(id, b.Text), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
        c.MapPost("/{id:guid}/cv", async (Guid id, HttpRequest request, ISender s, CancellationToken ct) =>
                {
                    if (!request.HasFormContentType) return FileRequired.ToProblem();
                    var form = await request.ReadFormAsync(ct);
                    if (form.Files.GetFile("file") is not { } file) return FileRequired.ToProblem();
                    await using var stream = file.OpenReadStream();
                    return (await s.Send(new UploadCandidateCvCommand(id, stream, file.FileName), ct)).ToHttpResult();
                })
            .RequirePermission(page, PermissionAction.Update)
            .WithBodyLimit(CvLimit)
            .DisableAntiforgery();
        c.MapGet("/{id:guid}/cv/link", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetCandidateCvLinkQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
    }

    private static CandidateInput Input(CandidateBody b) => new(b.FullName, b.Phone, b.Email, b.Source, b.Japanese, b.ExpectedSalary);
}
