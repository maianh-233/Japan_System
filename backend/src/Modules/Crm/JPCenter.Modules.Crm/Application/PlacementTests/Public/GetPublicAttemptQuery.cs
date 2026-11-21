using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.PlacementTests.Public;

/// <summary>CRM-13 bước 2 — mở link: thông tin bài, còn hạn hay không (E1), đang làm thì tiếp tục với thời gian còn lại (E2)</summary>
public sealed record GetPublicAttemptQuery(string Token) : IQuery<PublicAttemptDto>;

public sealed class GetPublicAttemptHandler(PublicAttempts attempts, IClock clock) : IQueryHandler<GetPublicAttemptQuery, PublicAttemptDto>
{
    public async Task<Result<PublicAttemptDto>> Handle(GetPublicAttemptQuery query, CancellationToken ct)
    {
        var found = await attempts.FindAsync(query.Token, ct);
        if (found is null) return PublicAttempts.NotFound;
        var (attempt, test) = found.Value;
        var now = clock.UtcNow;
        if (attempt.Status == AttemptStatus.Sent && now > attempt.ExpiresAt) return PlacementAttemptErrors.Expired;
        return await attempts.ToDtoAsync(attempt, test, now, ct);
    }
}
