using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Integrations.Common;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Integrations.DisconnectIntegration;

/// <summary>TH-02 — ngắt kết nối: xóa cấu hình (kể cả khóa bí mật), tắt hết việc</summary>
public sealed record DisconnectIntegrationCommand(string Key) : ICommand<IntegrationDto>;

public sealed class DisconnectIntegrationHandler(ISystemDbContext db, IntegrationReader reader) : ICommandHandler<DisconnectIntegrationCommand, IntegrationDto>
{
    public async Task<Result<IntegrationDto>> Handle(DisconnectIntegrationCommand cmd, CancellationToken ct)
    {
        var integration = await db.Integrations.FirstOrDefaultAsync(i => i.Key == cmd.Key, ct);
        if (integration is null) return IntegrationErrors.Unknown;
        integration.Disconnect();
        await db.SaveChangesAsync(ct);
        return await reader.ToDtoAsync(integration, ct);
    }
}
