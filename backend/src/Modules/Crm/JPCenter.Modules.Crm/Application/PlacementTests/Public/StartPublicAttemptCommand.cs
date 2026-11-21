using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.PlacementTests.Public;

/// <summary>CRM-13 bước 2 — bắt đầu làm: quá hạn → E1, đã bắt đầu / đã nộp → E3. Trả đề (không đáp án) + đồng hồ.</summary>
public sealed record StartPublicAttemptCommand(string Token) : ICommand<PublicAttemptDto>;

public sealed class StartPublicAttemptHandler(ICrmDbContext db, PublicAttempts attempts, IClock clock) : ICommandHandler<StartPublicAttemptCommand, PublicAttemptDto>
{
    public async Task<Result<PublicAttemptDto>> Handle(StartPublicAttemptCommand cmd, CancellationToken ct)
    {
        var found = await attempts.FindAsync(cmd.Token, ct);
        if (found is null) return PublicAttempts.NotFound;
        var (attempt, test) = found.Value;
        var now = clock.UtcNow;
        var started = attempt.Start(now);
        await db.SaveChangesAsync(ct);
        if (started.IsFailure) return started.Error;
        return await attempts.ToDtoAsync(attempt, test, now, ct);
    }
}
