namespace JPCenter.Application.Common.Security;

/// <summary>Thông tin kỹ thuật của request đang chạy — ghi vào audit / phiên đăng nhập. Ngoài HTTP → "system".</summary>
public interface IRequestInfo
{
    string Ip { get; }

    /// <summary>Thiết bị / trình duyệt rút gọn, vd "Chrome · Windows"</summary>
    string Device { get; }

    string? TraceId { get; }
}
