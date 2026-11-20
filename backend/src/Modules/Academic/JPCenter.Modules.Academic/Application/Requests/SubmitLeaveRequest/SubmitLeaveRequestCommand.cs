using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.SubmitLeaveRequest;

/// <summary>
/// DT-18 bước 1–2 — HV (hoặc PH gửi thay con, <paramref name="StudentId"/> bắt buộc khi có nhiều con) xin nghỉ 1 buổi chưa bắt đầu,
/// lý do ≥ 5 ký tự, tùy chọn đăng ký học bù ở lớp khác (<see cref="MakeupChecker"/>). Đã có đơn chờ / đã duyệt cho buổi đó (E2) → trùng.
/// </summary>
public sealed record SubmitLeaveRequestCommand(Guid? StudentId, Guid SessionId, string ReasonType, string Reason, Guid? MakeupSessionId)
    : ICommand<LeaveRequestDto>;

public sealed class SubmitLeaveRequestValidator : AbstractValidator<SubmitLeaveRequestCommand>
{
    public SubmitLeaveRequestValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ReasonType).NotEmpty().WithMessage("Chọn loại lý do").MaximumLength(100);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Nhập chi tiết lý do").MaximumLength(1000);
    }
}

public sealed class SubmitLeaveRequestHandler(IAcademicDbContext db, LeaveAccess leaves, MakeupChecker makeups, PortalOwner owner,
    RequestReader reader, IClock clock) : ICommandHandler<SubmitLeaveRequestCommand, LeaveRequestDto>
{
    public async Task<Result<LeaveRequestDto>> Handle(SubmitLeaveRequestCommand cmd, CancellationToken ct)
    {
        var found = await leaves.SessionAsync(cmd.StudentId, cmd.SessionId, ct);
        if (found.IsFailure) return found.Error;
        var (studentId, session, cls) = found.Value;
        if (session.Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;

        var duplicate = await db.LeaveRequests.AnyAsync(l => l.StudentId == studentId && l.SessionId == session.Id
            && (l.Status == RequestStatus.Pending || l.Status == RequestStatus.Approved), ct);
        if (duplicate) return RequestErrors.LeaveDuplicate;

        ClassSessionId? makeupId = null;
        if (cmd.MakeupSessionId is { } mid)
        {
            var candidate = await db.ClassSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new ClassSessionId(mid), ct);
            if (candidate is null) return RequestErrors.MakeupInvalid;
            if (await makeups.CheckAsync(session, cls, candidate, studentId, ct) is { IsFailure: true } r) return r.Error;
            makeupId = candidate.Id;
        }

        var created = LeaveRequest.Create(studentId, cls.Id, session.Id, session.BranchId, cmd.ReasonType, cmd.Reason, owner.By, owner.AccountId,
            owner.Name, makeupId, session.StartsAt, clock.LocalNow, clock.UtcNow);
        if (created.IsFailure) return created.Error;
        db.LeaveRequests.Add(created.Value);
        await db.SaveChangesAsync(ct);
        return (await reader.LeaveDtosAsync([created.Value], ct))[0];
    }
}
