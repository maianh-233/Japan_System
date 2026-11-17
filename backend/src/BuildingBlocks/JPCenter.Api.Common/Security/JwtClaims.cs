namespace JPCenter.Api.Common.Security;

/// <summary>Claim trong access token — chỉ thông tin ít đổi; quyền KHÔNG nằm trong token (nạp từ cache).</summary>
public static class JwtClaims
{
    /// <summary>Id tài khoản</summary>
    public const string Subject = "sub";
    /// <summary>staff | teacher | student | parent</summary>
    public const string Portal = "portal";
    /// <summary>Phiên bản token của tài khoản — lệch = token đã bị thu hồi</summary>
    public const string TokenVersion = "tv";
    /// <summary>Id phiên đăng nhập (thiết bị)</summary>
    public const string SessionId = "sid";
}
