using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>
/// Thu hồi phiên của 1 tài khoản: refresh token hết dùng được + access token đang lưu hành bị chặn ngay (danh sách thu hồi).
/// Người gọi tự SaveChanges.
/// </summary>
public sealed class SessionRevoker(ISystemDbContext db, ISessionRevocationList revoked, IClock clock)
{
    /// <param name="reason">logout · logout_others · password_changed · account_locked · token_reuse · admin</param>
    public async Task<int> RevokeAllAsync(AccountKind kind, Guid accountId, string reason, Guid? exceptSessionId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var sessions = await db.UserSessions
            .Where(x => x.AccountKind == kind && x.AccountId == accountId && x.RevokedAt == null)
            .ToListAsync(ct);
        var count = 0;
        foreach (var s in sessions.Where(s => s.Id.Value != exceptSessionId))
        {
            s.Revoke(reason, now);
            revoked.Revoke(s.Id.Value);
            count++;
        }
        return count;
    }
}
