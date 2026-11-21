using System.Text.Json;
using System.Text.Json.Serialization;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Infrastructure.Common.Persistence.Conversions;

/// <summary><see cref="Money"/> ↔ số JSON (đồng) trong cột <c>jsonb</c> — vd lịch đóng đợt của hóa đơn <c>[{amount, dueDate}]</c>.</summary>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Money.FromTrusted(reader.GetDecimal());

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Amount);
}
