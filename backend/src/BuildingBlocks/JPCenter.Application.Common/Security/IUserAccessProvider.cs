namespace JPCenter.Application.Common.Security;

/// <summary>Nạp quyền hiệu lực của tài khoản từ DB — module System hiện thực. Không tồn tại → null.</summary>
public interface IUserAccessProvider
{
    Task<UserAccess?> LoadAsync(Guid accountId, bool isPortalAccount, CancellationToken ct);
}
