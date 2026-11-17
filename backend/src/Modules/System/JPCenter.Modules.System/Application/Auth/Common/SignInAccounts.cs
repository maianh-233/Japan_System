using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Common;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.Modules.System.Domain.Sessions;
using JPCenter.Modules.System.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>Tìm tài khoản đăng nhập (AUTH-01 bước 3): trước tiên email nhân viên, không có thì tên đăng nhập / email cổng.</summary>
public sealed class SignInAccounts(ISystemDbContext db)
{
    /// <summary>Chuẩn hóa định danh: bỏ khoảng trắng, chữ thường (AUTH-01 bước 2).</summary>
    public static string Normalize(string identifier) => identifier.Trim().ToLowerInvariant();

    public async Task<ISignInAccount?> FindByLoginAsync(string identifier, CancellationToken ct)
    {
        var login = Normalize(identifier);
        var staff = await db.UserAccounts.FirstOrDefaultAsync(x => x.Email == login, ct);
        if (staff is not null) return staff;
        return await db.PortalAccounts.FirstOrDefaultAsync(x => x.Username == login || x.Email == login, ct);
    }

    /// <summary>Tìm theo email (quên mật khẩu — chỉ email, không nhận tên đăng nhập).</summary>
    public async Task<ISignInAccount?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var e = Normalize(email);
        var staff = await db.UserAccounts.FirstOrDefaultAsync(x => x.Email == e, ct);
        if (staff is not null) return staff;
        return await db.PortalAccounts.FirstOrDefaultAsync(x => x.Email == e, ct);
    }

    public async Task<ISignInAccount?> FindAsync(AccountKind kind, Guid accountId, CancellationToken ct) => kind == AccountKind.Staff
        ? await db.UserAccounts.FirstOrDefaultAsync(x => x.Id == new UserAccountId(accountId), ct)
        : await db.PortalAccounts.FirstOrDefaultAsync(x => x.Id == new PortalAccountId(accountId), ct);
}
