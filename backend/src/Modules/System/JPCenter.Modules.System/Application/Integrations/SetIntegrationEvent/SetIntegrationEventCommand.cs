using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Integrations.Common;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Integrations.SetIntegrationEvent;

/// <summary>TH-03 — bật / tắt 1 việc dùng kênh. Bật khi chưa kết nối → <c>system.integration.not_connected</c>; việc lạ → <c>unknown_event</c>.</summary>
public sealed record SetIntegrationEventCommand(string Key, string Event, bool On) : ICommand<IntegrationDto>;

public sealed class SetIntegrationEventHandler(ISystemDbContext db, IntegrationReader reader) : ICommandHandler<SetIntegrationEventCommand, IntegrationDto>
{
    public async Task<Result<IntegrationDto>> Handle(SetIntegrationEventCommand cmd, CancellationToken ct)
    {
        var integration = await db.Integrations.FirstOrDefaultAsync(i => i.Key == cmd.Key, ct);
        if (integration is null) return IntegrationErrors.Unknown;
        var set = integration.SetEvent(cmd.Event, cmd.On);
        if (set.IsFailure) return set.Error;
        await db.SaveChangesAsync(ct);
        return await reader.ToDtoAsync(integration, ct);
    }
}
