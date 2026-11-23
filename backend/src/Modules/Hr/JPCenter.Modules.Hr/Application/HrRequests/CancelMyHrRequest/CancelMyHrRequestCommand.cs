using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.HrRequests.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.HrRequests.CancelMyHrRequest;

/// <summary>DN-03 — người gửi hủy đơn còn chờ duyệt (không phải người gửi → only_owner; đã xử lý → not_pending).</summary>
public sealed record CancelMyHrRequestCommand(Guid Id) : ICommand<HrRequestDto>;

public sealed class CancelMyHrRequestHandler(IHrDbContext db, HrRequestReader reader, CurrentEmployee current)
    : ICommandHandler<CancelMyHrRequestCommand, HrRequestDto>
{
    public async Task<Result<HrRequestDto>> Handle(CancelMyHrRequestCommand cmd, CancellationToken ct)
    {
        var request = await reader.FindForUpdateAsync(cmd.Id, ct);
        if (request is null) return HrRequestErrors.NotFound;
        if (current.Id is not { } me) return HrRequestErrors.OnlyOwner;
        var cancelled = request.Cancel(new EmployeeId(me));
        if (cancelled.IsFailure) return cancelled.Error;
        await db.SaveChangesAsync(ct);
        return await reader.DetailAsync(request, ct);
    }
}
