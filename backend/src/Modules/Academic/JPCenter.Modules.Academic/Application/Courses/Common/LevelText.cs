using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Courses.Common;

/// <summary>Cấp độ JLPT ↔ chữ giống frontend (<c>beginner</c>, <c>N5</c>…) — JSON chung đổi enum sang snake_case thường (<c>n5</c>)</summary>
public static class LevelText
{
    public static string Of(JlptLevel level) => level == JlptLevel.Beginner ? "beginner" : level.ToString();
}
