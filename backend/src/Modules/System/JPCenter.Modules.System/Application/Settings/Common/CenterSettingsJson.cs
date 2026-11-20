using System.Globalization;
using System.Text.Json;
using JPCenter.Modules.System.Domain.Settings;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Settings.Common;

/// <summary>
/// Đổi <see cref="CenterSettings"/> ↔ JSON, đúng hình dạng <c>CenterSettings</c> của frontend:
/// <list type="bullet">
/// <item>Lưu DB: mỗi mục 1 dòng <c>system.settings</c> (khóa "nhóm.mục" → giá trị JSON) — <see cref="ToRows"/> / <see cref="FromRows"/>.
/// Đọc từ DB: mục thiếu hoặc hỏng lấy mặc định (cài đặt hỏng không được làm sập nghiệp vụ).</item>
/// <item>API: object lồng <c>{ crm: { defaultSlaMinutes }, office: { … }, … }</c> — <see cref="ToObject"/> / <see cref="Parse"/>.
/// Nhận từ client: chặt — thiếu / sai kiểu → lỗi theo khóa "nhóm.mục".</item>
/// </list>
/// </summary>
public static class CenterSettingsJson
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Mọi khóa thuộc bộ Cài đặt chung (không gồm khóa khác như <c>academic.holidays</c>)</summary>
    public static readonly IReadOnlyList<string> Keys = CenterSettings.Labels.Keys.ToArray();

    private static readonly Dictionary<string, JsonElement> Defaults =
        CenterSettings.Default.ToEntries().ToDictionary(e => e.Key, e => JsonSerializer.SerializeToElement(e.Value, Json));

    /// <summary>Giá trị JSON của từng mục — để lưu DB</summary>
    public static IReadOnlyList<(string Key, string Value)> ToRows(CenterSettings settings) =>
        settings.ToEntries().Select(e => (e.Key, JsonSerializer.Serialize(e.Value, Json))).ToList();

    /// <summary>Object lồng theo nhóm — trả về API (frontend dùng thẳng làm <c>CenterSettings</c>)</summary>
    public static Dictionary<string, Dictionary<string, object?>> ToObject(CenterSettings settings)
    {
        var result = new Dictionary<string, Dictionary<string, object?>>();
        foreach (var e in settings.ToEntries())
        {
            var (group, field) = Split(e.Key);
            if (!result.TryGetValue(group, out var g)) result[group] = g = new Dictionary<string, object?>();
            g[field] = e.Value;
        }
        return result;
    }

    /// <summary>Dựng bộ cài đặt từ các dòng đã lưu; <paramref name="invalidKeys"/> = mục hỏng đã thay bằng mặc định (để ghi log).</summary>
    public static CenterSettings FromRows(IReadOnlyDictionary<string, string> rows, out IReadOnlyList<string> invalidKeys)
    {
        var values = new Dictionary<string, JsonElement>(Defaults);
        var bad = new List<string>();
        foreach (var (key, raw) in rows)
        {
            if (!Defaults.ContainsKey(key)) continue;
            if (ParseRaw(raw) is { } element) values[key] = element;
            else bad.Add(key);
        }

        var settings = Build(key => values[key], bad.Add);
        // Từng mục đúng kiểu nhưng cả bộ vi phạm ràng buộc (vd sửa tay from > to) → dùng mặc định cho cả bộ, báo mọi mục
        if (settings.Validate().IsFailure)
        {
            invalidKeys = Keys;
            return CenterSettings.Default;
        }
        invalidKeys = bad.Distinct().ToList();
        return settings;
    }

    /// <summary>
    /// Đọc bộ cài đặt client gửi (CD-02). Thiếu mục / sai kiểu → <c>validation.failed</c> kèm lỗi theo khóa "nhóm.mục";
    /// đúng kiểu thì kiểm tra nghiệp vụ bằng <see cref="CenterSettings.Validate"/>. <c>office.satTo</c> bỏ trống / null = nghỉ thứ 7.
    /// </summary>
    public static Result<CenterSettings> Parse(JsonElement body)
    {
        var values = new Dictionary<string, JsonElement>();
        if (body.ValueKind == JsonValueKind.Object)
        {
            foreach (var group in body.EnumerateObject().Where(g => g.Value.ValueKind == JsonValueKind.Object))
            foreach (var field in group.Value.EnumerateObject())
                values[$"{group.Name}.{field.Name}"] = field.Value;
        }

        var errors = new Dictionary<string, string[]>();
        var settings = Build(
            key => values.TryGetValue(key, out var v) ? v : key == "office.satTo" ? NullElement : null,
            key => errors[key] = [$"{CenterSettings.Labels[key]}: thiếu hoặc sai kiểu dữ liệu"]);
        if (errors.Count > 0) return Error.InvalidInput(errors);

        var valid = settings.Validate();
        return valid.IsSuccess ? settings : valid.Error;
    }

    private static readonly JsonElement NullElement = JsonSerializer.SerializeToElement<object?>(null);

    /// <summary>Dựng bộ cài đặt: <paramref name="get"/> trả giá trị của khóa (null = thiếu); mục thiếu / sai kiểu → gọi <paramref name="invalid"/> và lấy mặc định.</summary>
    private static CenterSettings Build(Func<string, JsonElement?> get, Action<string> invalid)
    {
        T Read<T>(string key, Func<JsonElement, T?> read) where T : struct
        {
            if (get(key) is { } e && read(e) is { } v) return v;
            invalid(key);
            return read(Defaults[key])!.Value;
        }

        string Text(string key)
        {
            if (get(key) is { ValueKind: JsonValueKind.String } e && e.GetString() is { Length: > 0 } s) return s;
            invalid(key);
            return Defaults[key].GetString()!;
        }

        // null hoặc chuỗi rỗng (ô giờ bỏ trống trên form) = không có
        TimeOnly? OptionalTime(string key)
        {
            var e = get(key);
            if (e is { ValueKind: JsonValueKind.Null } || (e is { ValueKind: JsonValueKind.String } s && s.GetString() == "")) return null;
            return Read(key, AsTime);
        }

        return new CenterSettings(
            new CrmSettings(Read("crm.defaultSlaMinutes", AsInt)),
            new OfficeSettings(Read("office.from", AsTime), Read("office.to", AsTime), OptionalTime("office.satTo"), Read("office.lateGraceMin", AsInt)),
            new InvoiceSettings(Text("invoice.codePattern"), Read("invoice.resetEvery", AsReset)),
            new PayrollSettings(Read("payroll.lockDay", AsInt)),
            new AtRiskSettings(Read("atRisk.absent", AsInt), Read("atRisk.streak", AsInt), Read("atRisk.attendanceRate", AsInt),
                Read("atRisk.submitRate", AsInt), Read("atRisk.scorePct", AsInt), Read("atRisk.mediumFrom", AsInt), Read("atRisk.highFrom", AsInt)),
            new ParentNoticeSettings(Read("parent.arrivalNotice", AsBool), Read("parent.absentAlertMin", AsInt), Read("parent.underAge", AsInt),
                Read("parent.weeklyReport", AsBool), Read("parent.weeklyDay", AsInt), Read("parent.weeklyTime", AsTime)),
            new SafetySettings(Money.FromTrusted(Read("safety.refundApprovalMin", AsMoney)), Read("safety.trashDays", AsInt)));
    }

    private static (string Group, string Field) Split(string key)
    {
        var dot = key.IndexOf('.');
        return (key[..dot], key[(dot + 1)..]);
    }

    private static JsonElement? ParseRaw(string raw)
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
