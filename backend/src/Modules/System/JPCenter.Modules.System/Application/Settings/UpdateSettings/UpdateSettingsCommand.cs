using System.Text.Json;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Settings.Common;
using JPCenter.Modules.System.Domain.Settings;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Settings.UpdateSettings;

/// <summary>CD-02 — lưu cả bộ Cài đặt chung (client gửi đủ 7 nhóm như <c>CenterSettings</c> frontend).</summary>
public sealed record UpdateSettingsCommand(JsonElement Settings) : ICommand<SettingsResponse>;

/// <summary>
/// Sai → không lưu mục nào (E1 → E4). Chỉ ghi mục đổi; không đổi gì → không ghi nhật ký (E5). Ghi đè từng mục, không chống sửa đè (E6).
/// Nhật ký: thay đổi theo nhãn tiếng Việt (bỏ thay đổi thô của EF — cột key / value bị che như dữ liệu bí mật).
/// </summary>
public sealed class UpdateSettingsHandler(ISystemDbContext db, SettingsStore store, SettingsCache cache, SettingsReader reader,
    IAuditTrail audit, IAuditChangeCollector changes, ICurrentUser user, IClock clock) : ICommandHandler<UpdateSettingsCommand, SettingsResponse>
{
    public async Task<Result<SettingsResponse>> Handle(UpdateSettingsCommand cmd, CancellationToken ct)
    {
        var parsed = CenterSettingsJson.Parse(cmd.Settings);
        if (parsed.IsFailure) return parsed.Error;
        var next = parsed.Value;

        var current = await store.LoadAsync(ct);
        var diff = current.Diff(next);
        if (diff.Count == 0) return await reader.ReadAsync(ct, 0);

        var keys = CenterSettingsJson.Keys;
        var rows = await db.Settings.Where(s => keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, ct);
        foreach (var (key, value) in CenterSettingsJson.ToRows(next))
        {
            if (rows.TryGetValue(key, out var row)) row.Change(value);
            else db.Settings.Add(Setting.Create(key, value));
        }
        await db.SaveChangesAsync(ct);
        cache.Set(next);

        changes.Drain();
        foreach (var c in diff) changes.Add(new AuditFieldChange(c.Field, c.Before, c.After));
        audit.Record(AuditCategory.Setting, AuditSeverity.Warning, SettingsReader.AuditAction, $"{diff.Count} mục");

        // Nhật ký ghi nền (chưa đọc lại được ngay) → trả người sửa / thời điểm của chính lần lưu này
        return new SettingsResponse(CenterSettingsJson.ToObject(next), clock.UtcNow, user.Access?.DisplayName, diff.Count);
    }
}
