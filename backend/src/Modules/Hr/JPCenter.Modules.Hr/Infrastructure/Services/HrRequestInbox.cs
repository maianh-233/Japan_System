using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Hr.Application.HrRequests.Common;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IHrRequestInbox"/> — lọc bằng <see cref="HrRequestReader.IsTodo"/> (đúng tab "Chờ tôi duyệt").</summary>
internal sealed class HrRequestInbox(HrDbContext db, HrRequestReader reader) : IHrRequestInbox
{
    public async Task<IReadOnlyList<PendingHrRequest>> PendingForMeAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct)
    {
        var query = db.HrRequests.AsNoTracking().Where(r => r.Status == HrRequestStatus.Pending);
        if (branchIds is not null)
        {
            var ids = branchIds.Select(b => new BranchId(b)).ToList();
            query = query.Where(r => ids.Contains(r.BranchId));
        }
        var todo = (await query.ToListAsync(ct)).Where(reader.IsTodo).ToList();
        if (todo.Count == 0) return [];
        var names = await reader.NamesAsync(ct);
        return todo.OrderBy(r => r.RequestedAt)
            .Select(r => new PendingHrRequest(r.Id.Value, r.Code, SnakeCaseEnumConverter<HrRequestType>.ToText(r.Type),
                names.GetValueOrDefault(r.EmployeeId.Value, "?"), r.BranchId.Value, r.RequestedAt))
            .ToList();
    }
}
