using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.AuditLogs.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.AuditLogs.GetAuditActors;

/// <summary>NK-01 — danh sách người đã thao tác (trong phạm vi được xem, 1 năm gần nhất), tên / vai trò theo lần gần nhất.</summary>
public sealed record GetAuditActorsQuery : IQuery<IReadOnlyList<AuditActorDto>>;

public sealed class GetAuditActorsHandler(AuditLogReader reader, IClock clock) : IQueryHandler<GetAuditActorsQuery, IReadOnlyList<AuditActorDto>>
{
    public async Task<Result<IReadOnlyList<AuditActorDto>>> Handle(GetAuditActorsQuery query, CancellationToken ct)
    {
        var since = AuditLogReader.StartOfDay(clock.Today.AddDays(-(AuditErrors.MaxRangeDays - 1)));
        var rows = await reader.Visible()
            .Where(a => a.ActorId != null && a.At >= since)
            .GroupBy(a => a.ActorId!.Value)
            .Select(g => g.OrderByDescending(a => a.At).Select(a => new AuditActorDto(g.Key, a.ActorName, a.ActorRole)).First())
            .ToListAsync(ct);
        return rows.OrderBy(r => r.Name).ToList();
    }
}
