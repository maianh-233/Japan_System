namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>Trạng thái kết nối</summary>
public enum IntegrationStatus
{
    /// <summary>Đã kết nối</summary>
    Connected,
    /// <summary>Lỗi</summary>
    Error,
    /// <summary>Chưa kết nối</summary>
    Disconnected,
}
