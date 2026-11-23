using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Application.Common.Time;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Recruitment.Candidates.ScheduleInterview;

/// <summary>TD-03 — hẹn / sửa lịch phỏng vấn (thời gian, người phỏng vấn, hình thức, điểm 0–10, nhận xét). Người phỏng vấn nhận thông báo.</summary>
public sealed record ScheduleInterviewCommand(Guid Id, DateTimeOffset At, Guid InterviewerId, InterviewMode Mode, decimal? Score, string? Note)
    : ICommand<CandidateResult>;

public sealed class ScheduleInterviewValidator : AbstractValidator<ScheduleInterviewCommand>
{
    public ScheduleInterviewValidator() => RuleFor(x => x.Note).MaximumLength(2000);
}

public sealed class ScheduleInterviewHandler(IHrDbContext db, RecruitmentDesk desk, HrNotices notices)
    : ICommandHandler<ScheduleInterviewCommand, CandidateResult>
{
    public static readonly Error InterviewerInvalid = Error.Validation("hr.candidate.interviewer_invalid", "Người phỏng vấn phải là nhân sự đang làm việc");

    public async Task<Result<CandidateResult>> Handle(ScheduleInterviewCommand cmd, CancellationToken ct)
    {
        var found = await desk.FindCandidateAsync(cmd.Id, PermissionAction.Update, ct);
        if (found is null) return RecruitmentErrors.CandidateNotFound;
        var (c, job) = found.Value;
        var interviewer = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == new EmployeeId(cmd.InterviewerId), ct);
        if (interviewer is null || interviewer.HasQuit) return InterviewerInvalid;

        var changed = c.InterviewAt != cmd.At.ToUniversalTime() || c.InterviewerId != cmd.InterviewerId;
        var done = c.ScheduleInterview(cmd.At.ToUniversalTime(), cmd.InterviewerId, cmd.Mode, cmd.Score, cmd.Note);
        if (done.IsFailure) return done.Error;
        if (changed)
            notices.ToEmployees([cmd.InterviewerId], "Lịch phỏng vấn mới",
                $"{c.FullName} — {job.Title} · {TimeText.Moment(cmd.At)} ({(cmd.Mode == InterviewMode.Online ? "online" : "trực tiếp")})", "info",
                HrNotices.RecruitmentPage, "hr.candidate.interview");
        await db.SaveChangesAsync(ct);
        return await desk.ResultAsync(c, job, null, [], ct);
    }
}
