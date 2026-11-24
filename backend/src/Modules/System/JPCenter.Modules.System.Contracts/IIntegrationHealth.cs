using JPCenter.Modules.System.Contracts.Dtos;

namespace JPCenter.Modules.System.Contracts;

/// <summary>Tình trạng tích hợp cho bảng điều khiển (user story 17) — không trả cấu hình / khóa bí mật.</summary>
public interface IIntegrationHealth
{
    Task<IReadOnlyList<FailingIntegration>> FailingAsync(CancellationToken ct);
}
