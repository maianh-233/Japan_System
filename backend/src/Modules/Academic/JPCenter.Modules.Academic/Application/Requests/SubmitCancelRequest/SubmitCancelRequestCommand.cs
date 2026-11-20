using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.SubmitCancelRequest;

/// <summary>
/// DT-16 bước 1 — GV (của lớp hoặc dạy thay buổi đó) xin hủy 1 buổi chưa bắt đầu: loại lý do, chi tiết ≥ 10 ký tự, đề xuất ngày dạy bù.
/// Trùng đơn đang chờ (E7) → <c>academic.cancel_request.duplicate</c>.
/// </summary>
public sealed record SubmitCancelRequestCommand(Guid SessionId, string ReasonType, string Reason, DateTimeOffset? MakeupAt) : ICommand<CancelRequestDto>;

public sealed class SubmitCancelRequestValidator : AbstractValidator<SubmitCancelRequestCommand>
{
    public SubmitCancelRequestValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ReasonType).NotEmpty().WithMessage("Chọn loại lý do").MaximumLength(100);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Nhập chi tiết lý do").MaximumLength(1000);
    }
}

public sealed class SubmitCancelRequestHandler(IAcademicDbContext db, TeacherSessions teacher, RequestReader reader, IClock clock,
    AcademicNotices notices)
    : ICommandHandler<SubmitCancelRequestCommand, CancelRequestDto>
{
    public async Task<Result<CancelRequestDto>> Handle(SubmitCancelRequestCommand cmd, CancellationToken ct)
    {
        var found = await teacher.ForUpdateAsync(cmd.SessionId, ct);
        if (found.IsFailure) return found.Error;
        var (session, cls) = found.Value;
        if (TeacherSessions.RoleOf(session, cls, teacher.Me) is not (StaffRole.Teacher or StaffRole.Substitute)) return SessionErrors.NotAssigned;
        if (session.Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (session.Status != SessionStatus.Scheduled || session.IsConfirmed) return SessionErrors.NotScheduled;

        var duplicate = await db.SessionCancelRequests.AnyAsync(r =>
            r.SessionId == session.Id && r.TeacherId == teacher.Me && r.Status == RequestStatus.Pending, ct);
        if (duplicate) return RequestErrors.CancelDuplicate;

        var created = SessionCancelRequest.Create(session.Id, cls.Id, session.BranchId, teacher.Me, cmd.ReasonType, cmd.Reason, cmd.MakeupAt,
            session.StartsAt, clock.LocalNow, clock.UtcNow);
        if (created.IsFailure) return created.Error;
        db.SessionCancelRequests.Add(created.Value);
        notices.CancelRequestSubmitted(session.BranchId, await notices.SessionLabelAsync(cls.Id, session.Id, ct), cmd.Reason.Trim());
        await db.SaveChangesAsync(ct);
        return (await reader.CancelDtosAsync([created.Value], ct))[0];
    }
}
