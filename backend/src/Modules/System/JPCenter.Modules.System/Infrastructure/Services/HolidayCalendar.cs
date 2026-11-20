using System.Text.Json;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IHolidayCalendar"/>: đọc JSON <c>[{ "date": "2026-09-02", "name": "Quốc khánh" }]</c> trong settings.</summary>
internal sealed class HolidayCalendar(SystemDbContext db, ILogger<HolidayCalendar> logger) : IHolidayCalendar
{
    public const string Key = "academic.holidays";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<Holiday>> ListAsync(CancellationToken ct)
    {
        var raw = await db.Settings.AsNoTracking().Where(s => s.Key == Key).Select(s => s.Value).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(raw)) return [];
        try
        {
            return (JsonSerializer.Deserialize<List<Holiday>>(raw, Json) ?? []).OrderBy(h => h.Date).ToList();
        }
        catch (JsonException ex)
        {
            // Cài đặt hỏng không được làm sập việc xếp lịch — coi như chưa khai báo ngày lễ
            logger.LogWarning(ex, "Cài đặt {Key} không đúng định dạng", Key);
            return [];
        }
    }
}
