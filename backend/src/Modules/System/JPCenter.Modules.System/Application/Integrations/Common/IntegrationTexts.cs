using static JPCenter.Modules.System.Domain.Integrations.IntegrationKeys;

namespace JPCenter.Modules.System.Application.Integrations.Common;

/// <summary>Nhãn hiển thị việc (event) và lượt dùng 30 ngày — khớp <c>frontend/src/mocks/data/system.ts › MOCK_INTEGRATIONS</c></summary>
public static class IntegrationTexts
{
    private static readonly Dictionary<(string Key, string Event), string> Events = new()
    {
        [(ZaloOa, "attendance")] = "Báo vắng học cho phụ huynh",
        [(ZaloOa, "tuition")] = "Nhắc học phí sắp đến hạn / quá hạn",
        [(ZaloOa, "schedule")] = "Đổi lịch / hủy buổi học",
        [(ZaloOa, "result")] = "Kết quả kiểm tra",
        [(Sms, "otp")] = "OTP đăng nhập / quên mật khẩu",
        [(Sms, "fallback")] = "Gửi thay khi Zalo lỗi",
        [(Email, "invoice")] = "Hóa đơn & phiếu thu",
        [(Email, "invite")] = "Mời tạo tài khoản",
        [(Email, "payslip")] = "Phiếu lương khi chốt bảng lương",
        [(Email, "test")] = "Link test xếp lớp",
        [(VietQr, "qr")] = "In mã QR trên hóa đơn",
        [(VietQr, "autoMatch")] = "Tự khớp giao dịch theo nội dung chuyển khoản",
        [(Zoom, "autoRoom")] = "Tự tạo phòng khi lên lịch buổi online",
        [(Zoom, "record")] = "Lưu bản ghi vào tài liệu lớp",
        [(Meet, "autoRoom")] = "Tự tạo phòng khi lên lịch buổi online",
        [(FacebookLead, "import")] = "Tạo lead tự động",
        [(FacebookLead, "dedupe")] = "Bỏ qua nếu trùng SĐT (ghi chú vào lead cũ)",
        [(WebForm, "import")] = "Tạo lead tự động",
        [(WebForm, "captcha")] = "Bắt buộc captcha",
        [(TikTokLead, "import")] = "Tạo lead tự động",
    };

    private static readonly Dictionary<string, string> Usage = new()
    {
        [ZaloOa] = "tin ZNS đã gửi", [Sms] = "SMS đã gửi", [Email] = "email đã gửi", [VietQr] = "giao dịch nhận", [Zoom] = "phòng đã tạo",
        [Meet] = "phòng đã tạo", [FacebookLead] = "lead nhận về", [WebForm] = "lead nhận về", [TikTokLead] = "lead nhận về",
    };

    public static string EventLabel(string key, string evt) => Events.GetValueOrDefault((key, evt), evt);

    public static string UsageLabel(string key) => Usage.GetValueOrDefault(key, "lượt dùng");
}
