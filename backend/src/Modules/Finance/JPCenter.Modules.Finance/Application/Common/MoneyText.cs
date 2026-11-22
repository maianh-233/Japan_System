using System.Globalization;

namespace JPCenter.Modules.Finance.Application.Common;

/// <summary>Số tiền dạng chữ cho tiêu đề phiếu duyệt / nhật ký: 2.000.000₫ (giống <c>formatCurrency</c> frontend)</summary>
public static class MoneyText
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Vnd(decimal amount) => amount.ToString("#,0", Vi) + "₫";
}
