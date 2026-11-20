using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Application.Settings.Common;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Settings;
using JPCenter.Modules.System.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Infrastructure.Persistence.Seed;

/// <summary>
/// Cài đặt chung mặc định (23 mục của user story 10 + ngày lễ) — thêm từng khóa còn thiếu (không ghi đè giá trị đã sửa), nên chạy được trên DB đã có dữ liệu.
/// Ngày lễ lấy từ <c>HOLIDAYS</c> ở <c>frontend/src/features/academic/classes/constants.ts</c>.
/// </summary>
internal sealed class SettingsSeeder(SystemDbContext db) : IDataSeeder
{
    public int Order => 21;

    private static readonly Holiday[] Holidays =
    [
        new(new(2026, 1, 1), "Tết Dương lịch"),
        new(new(2026, 4, 30), "Giải phóng miền Nam"),
        new(new(2026, 5, 1), "Quốc tế Lao động"),
        new(new(2026, 9, 1), "Quốc khánh (nghỉ bù)"),
        new(new(2026, 9, 2), "Quốc khánh"),
        new(new(2027, 1, 1), "Tết Dương lịch"),
        .. Enumerable.Range(4, 7).Select(d => new Holiday(new(2027, 2, d), "Tết Nguyên đán")),
        new(new(2027, 4, 16), "Giỗ Tổ Hùng Vương"),
        new(new(2027, 4, 30), "Giải phóng miền Nam"),
        new(new(2027, 5, 1), "Quốc tế Lao động"),
        new(new(2027, 9, 2), "Quốc khánh"),
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        var existing = (await db.Settings.Select(s => s.Key).ToListAsync(ct)).ToHashSet();

        if (!existing.Contains(HolidayCalendar.Key))
            db.Settings.Add(Setting.Create(HolidayCalendar.Key, JsonSerializer.Serialize(Holidays, new JsonSerializerOptions(JsonSerializerDefaults.Web))));

        // Cài đặt chung (user story 10): lưu sẵn giá trị mặc định để Adminer / trang cài đặt thấy đủ 23 mục
        foreach (var (key, value) in CenterSettingsJson.ToRows(CenterSettings.Default).Where(r => !existing.Contains(r.Key)))
            db.Settings.Add(Setting.Create(key, value));

        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(ct);
    }
}
