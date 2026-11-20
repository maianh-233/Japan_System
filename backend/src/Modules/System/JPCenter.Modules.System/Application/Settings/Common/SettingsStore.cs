using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.System.Application.Settings.Common;

/// <summary>Đọc Cài đặt chung (qua cache) — dùng trong module System và làm nguồn cho <c>ICenterSettings</c>.</summary>
public sealed class SettingsStore(ISystemDbContext db, SettingsCache cache, ILogger<SettingsStore> logger)
{
    public async Task<CenterSettings> GetAsync(CancellationToken ct)
    {
        if (cache.Value is { } cached) return cached;
        var settings = await LoadAsync(ct);
        cache.Set(settings);
        return settings;
    }

    /// <summary>Đọc thẳng DB (bỏ qua cache) — dùng khi lưu để so sánh trước → sau</summary>
    public async Task<CenterSettings> LoadAsync(CancellationToken ct)
    {
        var keys = CenterSettingsJson.Keys;
        var rows = await db.Settings.AsNoTracking().Where(s => keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var settings = CenterSettingsJson.FromRows(rows, out var invalid);
        if (invalid.Count > 0) logger.LogWarning("Cài đặt chung hỏng, dùng giá trị mặc định cho {Keys}", invalid);
        return settings;
    }
}
