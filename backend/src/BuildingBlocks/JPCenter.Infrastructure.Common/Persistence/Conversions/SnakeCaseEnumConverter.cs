using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JPCenter.Infrastructure.Common.Persistence.Conversions;

/// <summary>Enum ↔ chuỗi snake_case (vd <c>TestScheduled</c> ↔ <c>"test_scheduled"</c>) — giống giá trị frontend.</summary>
public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(v => ToDb[v], v => FromDb[v])
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> ToDb = Enum.GetValues<TEnum>().ToDictionary(v => v, v => ToSnakeCase(v.ToString()));
    private static readonly Dictionary<string, TEnum> FromDb = ToDb.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Giá trị lưu DB của 1 enum.</summary>
    public static string ToText(TEnum value) => ToDb[value];

    /// <summary>Đọc chuỗi snake_case (không phân biệt hoa thường, vd "N5", "part_time") thành enum.</summary>
    public static TEnum Parse(string text) =>
        FromDb.TryGetValue(ToSnakeCase(text.Replace("-", "_", StringComparison.Ordinal)).ToLowerInvariant(), out var v)
            ? v
            : throw new FormatException($"'{text}' không phải giá trị hợp lệ của {typeof(TEnum).Name}");

    public static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]))) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}

/// <summary>Danh sách enum (vd <c>List&lt;JlptLevel&gt;</c>) ↔ cột <c>text[]</c> giá trị snake_case.</summary>
public sealed class EnumListConverter<TEnum>() : ValueConverter<List<TEnum>, string[]>(
    v => v.Select(SnakeCaseEnumConverter<TEnum>.ToText).ToArray(),
    v => v.Select(SnakeCaseEnumConverter<TEnum>.Parse).ToList())
    where TEnum : struct, Enum;
