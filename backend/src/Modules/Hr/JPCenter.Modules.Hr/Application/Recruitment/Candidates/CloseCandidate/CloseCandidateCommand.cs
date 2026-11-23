using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Recruitment.Candidates.CloseCandidate;

/// <summary>Kết thúc hồ sơ ứng viên</summary>
public enum CandidateOutcome
{
    /// <summary>TD-04 — nhận việc (chỉ từ Offer)</summary>
    Hire,
    /// <summary>TD-04 — loại (lý do bắt buộc, mọi giai đoạn trừ Nhận việc)</summary>
    Reject,
    /// <summary>TD-03 — ứng viên từ chối offer → Loại, lý do "Từ chối offer"</summary>
    OfferDeclined,
}

/// <summary>TD-03 / TD-04 — nhận việc / loại / từ chối offer.</summary>
public sealed record CloseCandidateCommand(Guid Id, CandidateOutcome Outcome, string? Reason) : ICommand<CandidateResult>;

public sealed class CloseCandidateValidator : AbstractValidator<CloseCandidateCommand>
{
    public CloseCandidateValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

/// <summary>
/// Nhận việc: tạo phiếu onboarding (OB-01) — ngày bắt đầu = ngày offer, người hướng dẫn = người thao tác (thông báo khi khác người tạo);
/// đủ số lượng cần tuyển → tự đóng tin. ⏳ Email cảm ơn khi loại (giai đoạn 22).
/// </summary>
public sealed class CloseCandidateHandler(IHrDbContext db, RecruitmentDesk desk, CurrentEmployee current, IAuditTrail audit, IClock clock)
    : ICommandHandler<CloseCandidateCommand, CandidateResult>
{
    public static readonly Error NoMentor = Error.Forbidden("hr.onboarding.mentor_required",
        "Tài khoản của bạn chưa gắn hồ sơ nhân sự — không làm người hướng dẫn được");

    public async Task<Result<CandidateResult>> Handle(CloseCandidateCommand cmd, CancellationToken ct)
    {
        var found = await desk.FindCandidateAsync(cmd.Id, PermissionAction.Update, ct);
        if (found is null) return RecruitmentErrors.CandidateNotFound;
        var (c, job) = found.Value;
        var warnings = new List<string>();
        Guid? onboardingId = null;

        switch (cmd.Outcome)
        {
            case CandidateOutcome.Reject:
                if (c.Reject(cmd.Reason ?? "") is { IsFailure: true } r) return r.Error;
                break;
            case CandidateOutcome.OfferDeclined:
                if (c.DeclineOffer() is { IsFailure: true } d) return d.Error;
                break;
            default:
                if (current.Id is not { } mentor) return NoMentor;
                if (c.Hire() is { IsFailure: true } h) return h.Error;
                var onboarding = Onboarding.Create(c.Id, c.FullName, c.Phone, c.Email, job.Title, job.Department, job.Kind, job.BranchId,
                    c.OfferStartDate ?? clock.Today, new EmployeeId(mentor));
                if (onboarding.IsFailure) return onboarding.Error;
                db.Onboardings.Add(onboarding.Value);
                onboardingId = onboarding.Value.Id.Value;
                var hired = await db.Candidates.CountAsync(x => x.JobId == job.Id && x.Stage == CandidateStage.Hired && x.Id != c.Id, ct) + 1;
                if (job.CloseIfFilled(hired)) warnings.Add($"Đã tuyển đủ {job.Quantity} người — tin \"{job.Title}\" tự đóng.");
                break;
        }

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Outcome == CandidateOutcome.Hire ? "Ứng viên nhận việc" : "Loại ứng viên",
            c.FullName, $"{job.Title}{(c.RejectReason is { } why ? " · " + why : "")}", branchId: job.BranchId.Value);
        return await desk.ResultAsync(c, job, onboardingId, warnings, ct);
    }
}
