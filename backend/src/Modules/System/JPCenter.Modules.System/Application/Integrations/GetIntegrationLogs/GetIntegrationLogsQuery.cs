using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Integrations.Common;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Integrations.GetIntegrationLogs;

/// <summary>TH-05 — 50 dòng nhật ký mới nhất của 1 tích hợp</summary>
public sealed record GetIntegrationLogsQuery(string Key) : IQuery<IReadOnlyList<IntegrationLogDto>>;

public sealed class GetIntegrationLogsHandler(ISystemDbContext db, IntegrationReader reader)
    : IQueryHandler<GetIntegrationLogsQuery, IReadOnlyList<IntegrationLogDto>>
{
    public async Task<Result<IReadOnlyList<IntegrationLogDto>>> Handle(GetIntegrationLogsQuery query, CancellationToken ct)
    {
        var integration = await db.Integrations.AsNoTracking().FirstOrDefaultAsync(i => i.Key == query.Key, ct);
        if (integration is null) return IntegrationErrors.Unknown;
        return Result.Success(await reader.LogsAsync(integration, ct));
    }
}
