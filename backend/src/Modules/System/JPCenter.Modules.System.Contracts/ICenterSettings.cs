using JPCenter.Modules.System.Contracts.Dtos;

namespace JPCenter.Modules.System.Contracts;

/// <summary>
/// Đọc Cài đặt chung (user story 10, CD-03). Có cache trong bộ nhớ (1 instance), xóa ngay khi Admin lưu —
/// gọi mỗi lần cần, không tự giữ bản sao lâu dài.
/// </summary>
public interface ICenterSettings
{
    Task<CenterSettingsSnapshot> GetAsync(CancellationToken ct);
}
