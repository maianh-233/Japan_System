using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Settings;

public static class SettingsErrors
{
    public static readonly Error OfficeHoursInvalid =
        Error.Validation("system.settings.office_hours_invalid", "Giờ hành chính không hợp lệ: giờ kết thúc (và giờ thứ 7) phải sau giờ bắt đầu");
    public static readonly Error InvoicePatternInvalid =
        Error.Validation("system.settings.invoice_pattern_invalid",
            "Mẫu mã hóa đơn không hợp lệ: tối đa 40 ký tự, chỉ dùng {CN} {YYYY} {YY} {MM} và đúng 1 biến số thứ tự {SO} / {SO:n}");
    public static readonly Error AtRiskLevelsInvalid =
        Error.Validation("system.settings.at_risk_levels_invalid", "Điểm rủi ro mức Trung bình phải nhỏ hơn mức Cao");
}
