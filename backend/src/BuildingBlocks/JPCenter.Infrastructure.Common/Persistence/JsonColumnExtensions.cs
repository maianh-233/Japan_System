using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Infrastructure.Common.Persistence;

/// <summary>Lưu 1 thuộc tính có cấu trúc (dictionary, danh sách record...) vào cột <c>jsonb</c> — database.md: "cấu trúc lồng: jsonb".</summary>
public static class JsonColumnExtensions
{
    /// <summary>JSON trong DB: camelCase, enum snake_case — giống JSON API.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property) where T : class
    {
        property.HasConversion(
            v => JsonSerializer.Serialize(v, Options),
            s => JsonSerializer.Deserialize<T>(s, Options)!,
            new ValueComparer<T>(
                (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(StringComparison.Ordinal),
                v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!));
        property.HasColumnType("jsonb");
        return property;
    }
}
