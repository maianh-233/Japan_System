using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JPCenter.Api.Common.Validation;

/// <summary>
/// Làm sạch MỌI chuỗi gửi lên (request-pipeline.md mục 6): chuẩn hóa Unicode NFC (quan trọng để so trùng tiếng Việt / tiếng Nhật)
/// và bỏ ký tự điều khiển (giữ xuống dòng, tab). KHÔNG tự Trim — mật khẩu có thể có khoảng trắng; trim ở validator / domain.
/// </summary>
public sealed class NormalizedStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value)) return value;

        var normalized = value.IsNormalized(NormalizationForm.FormC) ? value : value.Normalize(NormalizationForm.FormC);
        if (!normalized.Any(IsStripped)) return normalized;

        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
            if (!IsStripped(c)) sb.Append(c);
        return sb.ToString();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);

    private static bool IsStripped(char c) => char.IsControl(c) && c is not ('\n' or '\r' or '\t');
}
