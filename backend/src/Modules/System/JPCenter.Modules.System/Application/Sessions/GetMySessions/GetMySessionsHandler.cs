using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Sessions.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Sessions.GetMySessions;

public sealed class GetMySessionsHandler(ISystemDbContext db, ICurrentUser user, IClock clock)
    : IQueryHandler<GetMySessionsQuery, IReadOnlyList<MySessionDto>>
{
    public async Task<Result<IReadOnlyList<MySessionDto>>> Handle(GetMySessionsQuery query, CancellationToken ct)
    {
        if (MyAccount.Of(user) is not var (kind, id)) return Result.Success<IReadOnlyList<MySessionDto>>([]);
        var now = clock.UtcNow;
        var current = user.SessionId;

        var list = await db.UserSessions.AsNoTracking()
            .Where(x => x.AccountKind == kind && x.AccountId == id && x.RevokedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => new MySessionDto(x.Id.Value, x.Device, x.Ip, x.CreatedAt, x.LastSeenAt, x.Remember, x.Id.Value == current))
            .ToListAsync(ct);
        return list;
    }
}
