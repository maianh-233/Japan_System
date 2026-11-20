using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.WithdrawCancelRequest;

/// <summary>DT-16 bước 1 — GV thu hồi đơn hủy buổi của mình khi còn chờ duyệt</summary>
public sealed record WithdrawCancelRequestCommand(Guid Id) : ICommand;

public sealed class WithdrawCancelRequestHandler(IAcademicDbContext db, TeacherSessions teacher, IClock clock)
    : ICommandHandler<WithdrawCancelRequestCommand>
{
    public async Task<Result> Handle(WithdrawCancelRequestCommand cmd, CancellationToken ct)
    {
        var request = await db.SessionCancelRequests.FirstOrDefaultAsync(r => r.Id == new SessionCancelRequestId(cmd.Id) && r.TeacherId == teacher.Me, ct);
        if (request is null) return RequestErrors.CancelRequestNotFound;
        if (request.Withdraw(clock.UtcNow) is { IsFailure: true } r) return r;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
