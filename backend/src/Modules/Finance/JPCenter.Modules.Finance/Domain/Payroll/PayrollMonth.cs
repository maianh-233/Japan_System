using System.Globalization;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Tháng lương dạng chuỗi <c>YYYY-MM</c> (khớp frontend) — kiểm tra, khoảng ngày, hạn chốt.</summary>
public static class PayrollMonth
{
    /// <summary>Chuẩn hóa / kiểm tra tháng; sai → <see cref="PayrollErrors.InvalidMonth"/></summary>
    public static Result<string> Parse(string? month) =>
        TryFirstDay(month, out var first) ? Of(first) : PayrollErrors.InvalidMonth;

    /// <summary>Ngày đầu tháng</summary>
    public static bool TryFirstDay(string? month, out DateOnly first) =>
        DateOnly.TryParseExact((month ?? string.Empty).Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out first);

    /// <summary>Tháng chứa ngày <paramref name="day"/></summary>
    public static string Of(DateOnly day) => day.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>Ngày đầu, ngày cuối của tháng (tháng đã kiểm tra hợp lệ)</summary>
    public static (DateOnly From, DateOnly To) Range(string month)
    {
        if (!TryFirstDay(month, out var first)) throw new ArgumentException("Tháng không hợp lệ", nameof(month));
        return (first, first.AddMonths(1).AddDays(-1));
    }

    /// <summary>Hạn chốt = ngày <paramref name="lockDay"/> (Cài đặt <c>payroll.lockDay</c>, 1 → 28) của tháng SAU</summary>
    public static DateOnly LockDeadline(string month, int lockDay) => Range(month).From.AddMonths(1).AddDays(Math.Clamp(lockDay, 1, 28) - 1);

    /// <summary>Quá hạn chốt mà chưa chốt → nhãn cảnh báo (TC-13 bước 5)</summary>
    public static bool IsLockOverdue(string month, int lockDay, DateOnly today, bool locked) => !locked && today > LockDeadline(month, lockDay);
}
