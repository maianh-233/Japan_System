using System.Globalization;

namespace JPCenter.Modules.Academic.Application.Common;

/// <summary>Giờ học dạng "HH:mm" giống frontend</summary>
public static class TimeText
{
    public static string Of(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string? Of(TimeOnly? t) => t is { } v ? Of(v) : null;
}
