using JPCenter.Modules.System.Domain.Integrations;

namespace JPCenter.Modules.System.Application.Integrations.Common;

/// <summary>
/// Tích hợp — khớp <c>frontend/src/features/system/types.ts › Integration</c>. <c>id</c> = khóa cố định (<c>zalo-oa</c>…),
/// <c>config</c> đã che trường bí mật (TH-01.E2), <c>usage</c> đếm từ nhật ký 30 ngày.
/// </summary>
public sealed record IntegrationDto(
    string Id,
    string Name,
    IntegrationCategory Category,
    string Description,
    IntegrationStatus Status,
    string? Account,
    DateTimeOffset? ConnectedAt,
    string? ConnectedBy,
    DateTimeOffset? LastSyncAt,
    string? Error,
    IReadOnlyList<IntegrationFieldDto> Fields,
    IReadOnlyDictionary<string, string> Config,
    IReadOnlyList<IntegrationEventDto> Events,
    IntegrationUsageDto Usage,
    string? Webhook);

/// <summary>Trường cấu hình theo danh mục</summary>
public sealed record IntegrationFieldDto(string Key, string Label, bool Secret, bool Required);

/// <summary>Việc dùng kênh (tích = đang bật)</summary>
public sealed record IntegrationEventDto(string Key, string Label, bool On);

/// <summary>Lượt dùng 30 ngày (đếm nhật ký) và số lỗi</summary>
public sealed record IntegrationUsageDto(string Label, int Value, int Failed);

/// <summary>1 dòng nhật ký tích hợp — khớp <c>IntegrationLog</c> frontend</summary>
public sealed record IntegrationLogDto(Guid Id, string IntegrationId, DateTimeOffset At, bool Ok, string Message);
