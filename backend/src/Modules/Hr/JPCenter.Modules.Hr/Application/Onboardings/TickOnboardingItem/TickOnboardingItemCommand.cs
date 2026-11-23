using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Onboardings.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Onboardings.TickOnboardingItem;

/// <summary>OB-01 — tick / bỏ tick 1 mục checklist (ghi người + giờ).</summary>
public sealed record TickOnboardingItemCommand(Guid Id, string Key, bool Done) : ICommand<OnboardingDto>;

public sealed class TickOnboardingItemHandler(IHrDbContext db, OnboardingDesk desk, CurrentEmployee current, IClock clock)
    : ICommandHandler<TickOnboardingItemCommand, OnboardingDto>
{
    public async Task<Result<OnboardingDto>> Handle(TickOnboardingItemCommand cmd, CancellationToken ct)
    {
        var o = await desk.FindForUpdateAsync(cmd.Id, ct);
        if (o is null) return RecruitmentErrors.OnboardingNotFound;
        var ticked = o.Tick(cmd.Key, cmd.Done, current.Id ?? Guid.Empty, current.Name, clock.UtcNow);
        if (ticked.IsFailure) return ticked.Error;
        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(o, ct);
    }
}
