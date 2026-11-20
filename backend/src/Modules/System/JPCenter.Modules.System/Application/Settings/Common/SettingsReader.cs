using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Settings.Common;

/// <summary>Dựng <see cref="SettingsResponse"/>: lần sửa gần nhất lấy từ nhật ký "Đổi cài đặt chung" (đã lưu tên người sửa).</summary>
public sealed class SettingsReader(ISystemDbContext db, SettingsStore store)
{
    public const string AuditAction = "Đổi cài đặt chung";

    public async Task<SettingsResponse> ReadAsync(CancellationToken ct, int? changed = null)
    {
        var settings = await store.GetAsync(ct);
        var last = await db.AuditLogs.AsNoTracking()
            .Where(a => a.Category == AuditCategory.Setting && a.Action == AuditAction)
            .OrderByDescending(a => a.At)
            .Select(a => new { a.At, a.ActorName })
            .FirstOrDefaultAsync(ct);
        return new SettingsResponse(CenterSettingsJson.ToObject(settings), last?.At, last?.ActorName, changed);
    }
}
