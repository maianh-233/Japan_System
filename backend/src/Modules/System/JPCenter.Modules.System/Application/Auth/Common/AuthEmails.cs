using JPCenter.Application.Common.Email;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>
/// Nội dung email nhóm đăng nhập & tài khoản. Chỉ mô tả nội dung — khuôn thương hiệu áp ở <c>IEmailSender</c> (BuildingBlocks).
/// Giờ hiển thị theo giờ Việt Nam.
/// </summary>
public static class AuthEmails
{
    private static string Vn(DateTimeOffset at) => at.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm, dd/MM/yyyy");

    public static EmailContent ResetOtp(string otp) =>
        EmailContent.Create($"{otp} là mã đặt lại mật khẩu JP Center của bạn", "Mã xác thực đặt lại mật khẩu",
                $"Mã {otp} có hiệu lực trong 5 phút. Không chia sẻ mã này với bất kỳ ai.")
            .Hello(null)
            .Text("Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhập mã dưới đây để tiếp tục:")
            .Code(otp)
            .Text("Mã có hiệu lực trong **5 phút** và chỉ dùng được một lần.")
            .Notice("JP Center **không bao giờ** hỏi mã này qua điện thoại hay tin nhắn. Không chia sẻ mã cho bất kỳ ai, kể cả nhân viên trung tâm.",
                EmailTone.Warning)
            .Footnote("Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này — mật khẩu hiện tại vẫn an toàn.");

    public static EmailContent PasswordChanged(DateTimeOffset at, string device, string ip, string loginLink) =>
        EmailContent.Create("Mật khẩu tài khoản JP Center vừa được thay đổi", "Mật khẩu của bạn đã được thay đổi",
                $"Mật khẩu vừa được đổi lúc {Vn(at)}. Nếu không phải bạn, hãy liên hệ trung tâm ngay.")
            .Hello(null)
            .Text("Mật khẩu đăng nhập tài khoản JP Center của bạn vừa được thay đổi thành công.")
            .Details(("Thời gian", Vn(at)), ("Thiết bị", device), ("Địa chỉ IP", ip))
            .Text("Để bảo vệ tài khoản, các thiết bị khác đã được đăng xuất (nếu bạn chọn). Từ lần đăng nhập sau, vui lòng dùng mật khẩu mới.")
            .Button("Đăng nhập", loginLink)
            .Notice("**Không phải bạn thực hiện?** Liên hệ quản trị viên hoặc hotline trung tâm ngay để khóa tài khoản và kiểm tra.",
                EmailTone.Danger);

    public static EmailContent Invitation(string fullName, string roles, string link) =>
        EmailContent.Create("Mời kích hoạt tài khoản JP Center", "Chào mừng bạn đến với JP Center",
                "Tài khoản của bạn đã sẵn sàng — đặt mật khẩu để bắt đầu sử dụng hệ thống.")
            .Hello(fullName)
            .Text("Trung tâm đã tạo tài khoản trên hệ thống quản lý JP Center cho bạn. Hãy đặt mật khẩu để kích hoạt và bắt đầu làm việc.")
            .Details(("Vai trò", roles), ("Hiệu lực đường dẫn", "72 giờ, dùng 1 lần"))
            .Button("Đặt mật khẩu & kích hoạt", link)
            .Text("Mật khẩu cần tối thiểu **8 ký tự**, có cả chữ và số.")
            .Footnote("Đường dẫn hết hạn hoặc đã dùng? Liên hệ quản trị viên để được gửi lại lời mời. Nếu bạn không phải người được mời, hãy bỏ qua email này.");

    public static EmailContent LockoutWarning(int times, DateTimeOffset at, string device, string ip) =>
        EmailContent.Create("Cảnh báo bảo mật: nhiều lần đăng nhập sai vào tài khoản JP Center", "Có người đang thử đăng nhập vào tài khoản của bạn",
                $"Tài khoản bị khóa tạm {times} lần trong 24 giờ qua do nhập sai mật khẩu.")
            .Hello(null)
            .Text($"Tài khoản của bạn đã bị **khóa tạm {times} lần** trong 24 giờ qua do nhập sai mật khẩu nhiều lần liên tiếp.")
            .Details(("Lần gần nhất", Vn(at)), ("Thiết bị", device), ("Địa chỉ IP", ip))
            .Text("Nếu đó là bạn (quên mật khẩu), hãy dùng chức năng **Quên mật khẩu** trên trang đăng nhập để đặt lại.")
            .Notice("**Không phải bạn?** Đổi mật khẩu ngay sau khi đăng nhập và báo quản trị viên để kiểm tra.", EmailTone.Danger);

    public static EmailContent TemporaryPassword(string displayName, string username, string password, string loginLink) =>
        EmailContent.Create("Thông tin đăng nhập Cổng học viên / phụ huynh JP Center", "Tài khoản của bạn đã sẵn sàng",
                "Tên đăng nhập và mật khẩu tạm để vào Cổng học viên / phụ huynh JP Center.")
            .Hello(displayName)
            .Text("Trung tâm đã cấp tài khoản để bạn theo dõi lịch học, điểm danh, bài tập và học phí trên Cổng học viên / phụ huynh.")
            .Details(("Tên đăng nhập", username))
            .Code(password, "Mật khẩu tạm")
            .Button("Đăng nhập", loginLink)
            .Notice("Lần đăng nhập đầu tiên, hệ thống sẽ yêu cầu bạn **đổi mật khẩu**. Không chia sẻ mật khẩu cho người khác.")
            .Footnote("Nếu bạn không đăng ký học tại JP Center, hãy bỏ qua email này hoặc báo cho trung tâm.");
}
