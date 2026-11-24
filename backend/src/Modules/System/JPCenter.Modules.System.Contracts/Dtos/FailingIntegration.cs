namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>Tích hợp đang ở trạng thái lỗi (BC-01 › Cần xử lý)</summary>
/// <param name="Key">Khóa tích hợp (vd <c>zalo-oa</c>)</param>
/// <param name="Error">Lỗi gần nhất</param>
public sealed record FailingIntegration(string Key, string Name, string? Error);
