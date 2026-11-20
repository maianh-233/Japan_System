using System.Globalization;
using System.Text.Json;
using JPCenter.Modules.System.Domain.Settings;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Settings.Common;

/// <summary>
/// Đổi <see cref="CenterSettings"/> ↔ các dòng <c>system.settings</c> (khóa "nhóm.mục" → giá trị JSON).
/// Đọc: mục thiếu hoặc hỏng (sai kiểu, sửa tay trong DB) lấy giá trị mặc định — cài đặt hỏng không được làm sập nghiệp vụ.
/// </summary>
public static class CenterSettingsJson
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Mọi khóa thuộc bộ Cài đặt chung (không gồm khóa khác như <c>academic.holidays</c>)</summary>
    public static readonly IReadOnlyList<string> Keys = CenterSettings.Labels.Keys.ToArray();

    /// <summary>Giá trị JSON của từng mục</summary>
    public static IReadOnlyList<(string Key, string Value)> ToRows(CenterSettings settings) =>
        settings.ToEntries().Select(e => (e.Key, JsonSerializer.Serialize(e.Value, Json))).ToList();

    /// <summary>Dựng bộ cài đặt từ các dòng đã lưu; <paramref name="invalidKeys"/> = mục hỏng đã thay bằng mặc định (để ghi log).</summary>
    public static CenterSettings FromRows(IReadOnlyDictionary<string, string> rows, out IReadOnlyList<string> invalidKeys)
    {
        var defaults = ToRows(CenterSettings.Default).ToDictionary(r => r.Key, r => Parse(r.Value)!.Value);
        var bad = new List<string>();
        var values = new Dictionary<string, JsonElement>(defaults);
        foreach (var (key, raw) in rows)
        {
            if (!defaults.ContainsKey(key)) continue;
            if (Parse(raw) is { } element) values[key] = element;
            else bad.Add(key);
        }

        T Read<T>(string key, Func<JsonElement, T?> read) where T : struct
        {
            if (read(values[key]) is { } v) return v;
            bad.Add(key);
            return read(defaults[key])!.Value;
        }

        string Text(string key)
        {
            if (values[key].ValueKind == JsonValueKind.String && values[key].GetString() is { Length: > 0 } s) return s;
            bad.Add(key);
            return defaults[key].GetString()!;
        }

        TimeOnly? OptionalTime(string key)
        {
            if (values[key].ValueKind == JsonValueKind.Null) return null;
            return Read(key, AsTime);
        }

        var d = CenterSettings.Default;
        var settings = new CenterSettings(
            new CrmSettings(Read("crm.defaultSlaMinutes", AsInt)),
            new OfficeSettings(Read("office.from", AsTime), Read("office.to", AsTime), OptionalTime("office.satTo"), Read("office.lateGraceMin", AsInt)),
            new InvoiceSettings(Text("invoice.codePattern"), Read("invoice.resetEvery", AsReset)),
            new PayrollSettings(Read("payroll.lockDay", AsInt)),
            new AtRiskSettings(Read("atRisk.absent", AsInt), Read("atRisk.streak", AsInt), Read("atRisk.attendanceRate", AsInt),
                Read("atRisk.submitRate", AsInt), Read("atRisk.scorePct", AsInt), Read("atRisk.mediumFrom", AsInt), Read("atRisk.highFrom", AsInt)),
            new ParentNoticeSettings(Read("parent.arrivalNotice", AsBool), Read("parent.absentAlertMin", AsInt), Read("parent.underAge", AsInt),
                Read("parent.weeklyReport", AsBool), Read("parent.weeklyDay", AsInt), Read("parent.weeklyTime", AsTime)),
            new SafetySettings(Money.FromTrusted(Read("safety.refundApprovalMin", AsMoney)), Read("safety.trashDays", AsInt)));

        // Từng mục đúng kiểu nhưng cả bộ vi phạm ràng buộc (vd sửa tay from > to) → dùng mặc định cho cả bộ, báo mọi mục
        if (settings.Validate().IsFailure)
        {
            invalidKeys = Keys.ToList();
            return d;
        }

        invalidKeys = bad.Distinct().ToList();
        return settings;
    }

    private static JsonElement? Parse(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? AsInt(JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var v) ? v : null;

    private static decimal? AsMoney(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var v) && v >= 0 && decimal.Truncate(v) == v ? v : null;

    private static bool? AsBool(JsonElement e) => e.ValueKind is JsonValueKind.True or JsonValueKind.False ? e.GetBoolean() : null;

    private static TimeOnly? AsTime(JsonElement e) =>
        e.ValueKind == JsonValueKind.String && TimeOnly.TryParseExact(e.GetString(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t
            : null;

    private static InvoiceNumberReset? AsReset(JsonElement e) => e.ValueKind == JsonValueKind.String
        ? e.GetString() switch
        {
            "never" => InvoiceNumberReset.Never,
            "year" => InvoiceNumberReset.Year,
            "month" => InvoiceNumberReset.Month,
            _ => null,
        }
        : null;
}
