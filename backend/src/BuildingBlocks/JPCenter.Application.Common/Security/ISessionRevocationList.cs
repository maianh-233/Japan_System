namespace JPCenter.Application.Common.Security;

/// <summary>
/// Danh sách phiên vừa bị thu hồi (đăng xuất, đăng xuất từ xa — AUTH-03, AUTH-08). Access token của phiên đó bị từ chối NGAY,
/// không phải chờ hết hạn. Chỉ cần nhớ trong thời gian sống của access token (bộ nhớ trong — 1 instance).
/// </summary>
public interface ISessionRevocationList
{
    void Revoke(Guid sessionId);

    bool IsRevoked(Guid sessionId);
}
