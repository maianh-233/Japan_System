namespace JPCenter.Application.Common.Security;

/// <summary>Cache quyền theo tài khoản (bộ nhớ trong — 1 instance, không Redis).</summary>
public interface IUserAccessCache
{
    Task<UserAccess?> GetAsync(Guid accountId, bool isPortalAccount, CancellationToken ct);

    /// <summary>Gọi sau khi đổi role / quyền lẻ / khóa / đổi mật khẩu của tài khoản.</summary>
    void Invalidate(Guid accountId);

    /// <summary>Gọi sau khi sửa ma trận quyền của role (ảnh hưởng nhiều người).</summary>
    void InvalidateAll();
}
