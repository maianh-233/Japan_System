namespace JPCenter.Api.Common.Http;

/// <summary>
/// Tham số query kiểu enum viết snake_case như JSON (<c>?status=in_progress</c>) — minimal API mặc định chỉ nhận đúng tên C# (<c>InProgress</c>).
/// Dùng: <c>(QueryEnum&lt;RequestStatus&gt;? status) =&gt; ... status?.Value</c>. Giá trị lạ / số → 400.
/// </summary>
public readonly record struct QueryEnum<T>(T Value) where T : struct, Enum
{
    public static bool TryParse(string? value, IFormatProvider? provider, out QueryEnum<T> result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value) || char.IsDigit(value[0]) || value[0] == '-') return false;
        if (!Enum.TryParse<T>(value.Replace("_", "", StringComparison.Ordinal), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return false;
        result = new QueryEnum<T>(parsed);
        return true;
    }
}
