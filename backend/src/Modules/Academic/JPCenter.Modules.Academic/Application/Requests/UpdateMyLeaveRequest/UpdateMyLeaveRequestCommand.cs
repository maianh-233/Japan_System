using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.UpdateMyLeaveRequest;

/// <summary>DT-18 — HV / PH chọn / đổi / bỏ buổi học bù cho đơn còn chờ duyệt (<paramref name="MakeupSessionId"/> null = bỏ đăng ký bù)</summary>
public sealed record ChooseMakeupCommand(Guid Id, Guid? MakeupSessionId) : ICommand<LeaveRequestDto>;

/// <summary>DT-18 — HV / PH thu hồi đơn còn chờ duyệt</summary>
public sealed record WithdrawLeaveRequestCommand(Guid Id) : ICommand;

public sealed class UpdateMyLeaveRequestHandler(IAcademicDbContext db, LeaveAccess leaves, MakeupChecker makeups, RequestReader reader, IClock clock)
    : ICommandHandler<ChooseMakeupCommand, LeaveRequestDto>, ICommandHandler<WithdrawLeaveRequestCommand>
{
    public async Task<Result<LeaveRequestDto>> Handle(ChooseMakeupCommand cmd, CancellationToken ct)
    {
        var request = await leaves.OwnRequestAsync(cmd.Id, ct);
        if (request is null) return RequestErrors.LeaveRequestNotFound;
        if (!request.IsPending) return RequestErrors.NotPending;

        ClassSessionId? makeupId = null;
        if (cmd.MakeupSessionId is { } mid)
        {
            var original = await db.ClassSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
            var candidate = await db.ClassSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new ClassSessionId(mid), ct);
            if (original is null) return RequestErrors.SessionGone;
            if (candidate is null) return RequestErrors.MakeupInvalid;
            var cls = await db.Classes.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == original.ClassId, ct);
            if (await makeups.CheckAsync(original, cls, candidate, request.StudentId, ct) is { IsFailure: true } r) return r.Error;
            makeupId = candidate.Id;
        }

        if (request.ChangeMakeup(makeupId) is { IsFailure: true } changed) return changed.Error;
        await db.SaveChangesAsync(ct);
        return (await reader.LeaveDtosAsync([request], ct))[0];
    }

    public async Task<Result> Handle(WithdrawLeaveRequestCommand cmd, CancellationToken ct)
    {
        var request = await leaves.OwnRequestAsync(cmd.Id, ct);
        if (request is null) return RequestErrors.LeaveRequestNotFound;
        if (request.Withdraw(clock.UtcNow) is { IsFailure: true } r) return r;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
