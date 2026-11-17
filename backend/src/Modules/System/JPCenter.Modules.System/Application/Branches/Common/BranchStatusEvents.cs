using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.System.Contracts.IntegrationEvents;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.System.Application.Branches.Common;

/// <summary>Phát <see cref="BranchStatusChanged"/> cho module khác (gọi trước SaveChanges — Outbox).</summary>
public sealed class BranchStatusEvents(IIntegrationEventPublisher events, IClock clock)
{
    public void Changed(Branch branch, BranchStatus previous) =>
        events.Publish(new BranchStatusChanged(branch.Id.Value, branch.Code, branch.Name, BranchReader.StatusText(branch.Status),
            BranchReader.StatusText(previous), clock.UtcNow));
}
