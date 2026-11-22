using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JPCenter.Infrastructure.Common.Persistence.Conversions;

/// <summary>
/// Id strongly-typed (<c>record struct XxxId(Guid Value)</c>) trong cột jsonb ↔ chuỗi uuid (thay vì <c>{"value": …}</c>) —
/// vd tiêu chí bốc câu <c>{ lessonIds: ["…"] }</c>, câu hỏi của phần thi.
/// </summary>
public sealed class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type type) =>
        type is { IsValueType: true, IsPrimitive: false, IsEnum: false } && type != typeof(Guid)
        && type.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.PropertyType == typeof(Guid)
        && type.GetConstructor([typeof(Guid)]) is not null
        && type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length == 1;

    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(IdConverter<>).MakeGenericType(type))!;

    private sealed class IdConverter<TId> : JsonConverter<TId> where TId : struct
    {
        private static readonly ConstructorInfo Ctor = typeof(TId).GetConstructor([typeof(Guid)])!;
        private static readonly PropertyInfo Value = typeof(TId).GetProperty("Value")!;

        public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            (TId)Ctor.Invoke([reader.GetGuid()]);

        public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options) =>
            writer.WriteStringValue((Guid)Value.GetValue(value)!);
    }
}
