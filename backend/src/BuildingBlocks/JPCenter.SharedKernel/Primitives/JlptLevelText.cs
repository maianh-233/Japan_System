namespace JPCenter.SharedKernel.Primitives;

/// <summary>
/// Cấp độ JLPT ↔ chữ giống frontend (<c>beginner</c>, <c>N5</c> … <c>N1</c>). JSON chung đổi enum sang snake_case thường (<c>n5</c>),
/// nên DTO trả cấp độ dạng chuỗi qua hàm này. Đọc vào: enum nhận cả <c>N5</c> lẫn <c>n5</c>.
/// </summary>
public static class JlptLevelText
{
    public static string Of(JlptLevel level) => level == JlptLevel.Beginner ? "beginner" : level.ToString();

    public static JlptLevel? Parse(string? text) => Enum.TryParse<JlptLevel>(text, ignoreCase: true, out var l) ? l : null;
}
