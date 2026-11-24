using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.Modules.System.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IIntegrationHealth"/></summary>
internal sealed class IntegrationHealth(SystemDbContext db) : IIntegrationHealth
{
    public async Task<IReadOnlyList<FailingIntegration>> FailingAsync(CancellationToken ct) =>
        await db.Integrations.AsNoTracking()
            .Where(i => i.Status == IntegrationStatus.Error)
            .OrderBy(i => i.Name)
            .Select(i => new FailingIntegration(i.Key, i.Name, i.LastError))
            .ToListAsync(ct);
}
