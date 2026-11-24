using static JPCenter.Modules.System.Domain.Integrations.IntegrationKeys;

namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>Danh mục tích hợp hệ thống hỗ trợ (story 15, bảng "Khóa") — trường cấu hình + việc của từng khóa.</summary>
public static class IntegrationCatalog
{
    public static readonly IReadOnlyList<IntegrationSpec> All =
    [
        new(ZaloOa, IntegrationCategory.Notify,
            [new("oaId", "OA ID"), new("appId", "App ID"), new("secret", "Secret key", Secret: true)],
            ["attendance", "tuition", "schedule", "result"]),
        new(Sms, IntegrationCategory.Notify, [new("brandname", "Brandname"), new("apiKey", "API key", Secret: true)], ["otp", "fallback"]),
        new(Email, IntegrationCategory.Notify,
            [new("host", "SMTP host"), new("port", "Cổng"), new("user", "Tài khoản"), new("pass", "Mật khẩu", Secret: true)],
            ["invoice", "invite", "payslip", "test"]),
        new(VietQr, IntegrationCategory.Payment,
            [new("clientId", "Client ID"), new("apiKey", "API key", Secret: true), new("checksum", "Checksum key", Secret: true)],
            ["qr", "autoMatch"]),
        new(Zoom, IntegrationCategory.Meeting,
            [new("accountId", "Account ID"), new("clientId", "Client ID"), new("clientSecret", "Client secret", Secret: true)],
            ["autoRoom", "record"]),
        new(Meet, IntegrationCategory.Meeting, [new("domain", "Google Workspace domain")], ["autoRoom"]),
        new(FacebookLead, IntegrationCategory.Lead, [new("pageId", "Page ID"), new("token", "Page access token", Secret: true)], ["import", "dedupe"]),
        new(WebForm, IntegrationCategory.Lead, [new("domain", "Tên miền cho phép")], ["import", "captcha"]),
        new(TikTokLead, IntegrationCategory.Lead, [new("advertiserId", "Advertiser ID"), new("token", "Access token", Secret: true)], ["import"]),
    ];

    public static IntegrationSpec? Find(string key) => All.FirstOrDefault(s => s.Key == key);
}
