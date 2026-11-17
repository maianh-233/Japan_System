using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Auth.Common;
using JPCenter.Modules.System.Application.Sessions.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Sessions.GetLoginHistory;

/// <summary>Đọc từ audit_logs nhóm <c>auth</c> của chính mình (theo Id tài khoản hoặc email / tên đăng nhập bị thử).</summary>
public sealed class GetLoginHistoryHandler(ISystemDbContext db, ICurrentUser user, SignInAccounts accounts, IClock clock)
    : IQueryHandler<GetLoginHistoryQuery, LoginHistoryDto>
{
    public async Task<Result<LoginHistoryDto>> Handle(GetLoginHistoryQuery query, CancellationToken ct)
    {
        if (MyAccount.Of(user) is not var (kind, id)) return new LoginHistoryDto([], 0);
        var login = (await accounts.FindAsync(kind, id, ct))?.LoginName ?? "";
        var since = clock.UtcNow.AddDays(-30);

        var rows = await db.AuditLogs.AsNoTracking()
            .Where(x => x.Category == AuditCategory.Auth && x.At > since && (x.ActorId == id || x.Target == login))
            .OrderByDescending(x => x.At)
            .Take(50)
            .Select(x => new { x.At, x.Action, x.Ip, x.Device, x.Detail })
            .ToListAsync(ct);

        var items = rows.Select(r => new LoginHistoryItem(r.At, r.Action, !IsFailure(r.Action), r.Ip, r.Device, r.Detail)).ToList();
        var recentFailures = items.Where(i => i.Action.StartsWith("Đăng nhập", StringComparison.Ordinal))
            .TakeWhile(i => !i.Success).Count();
        return new LoginHistoryDto(items, recentFailures);
    }

    private static bool IsFailure(string action) =>
        action.Contains("thất bại", StringComparison.Ordinal) || action.StartsWith("Tạm khóa", StringComparison.Ordinal);
}
