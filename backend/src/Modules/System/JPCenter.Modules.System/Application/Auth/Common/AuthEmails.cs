using System.Net;

namespace JPCenter.Modules.System.Application.Auth.Common;

/// <summary>Nội dung email của nhóm đăng nhập (tiếng Việt, HTML đơn giản — mọi giá trị đều được mã hóa HTML).</summary>
public static class AuthEmails
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Layout(string title, string body) =>
        $"""
        <div style="font-family:Arial,sans-serif;max-width:520px;margin:auto;color:#1f2937">
          <h2 style="color:#b91c1c">JP Center</h2>
          <h3>{E(title)}</h3>
          {body}
          <p style="color:#6b7280;font-size:12px;margin-top:24px">Email tự động — vui lòng không trả lời.</p>
        </div>
        """;

    public static (string Subject, string Html) ResetOtp(string otp) => (
        $"Mã đặt lại mật khẩu: {otp}",
        Layout("Đặt lại mật khẩu",
            $"""
            <p>Mã xác thực của bạn là:</p>
            <p style="font-size:28px;font-weight:bold;letter-spacing:6px">{E(otp)}</p>
            <p>Mã có hiệu lực <b>5 phút</b>. Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.</p>
            """));

    public static (string Subject, string Html) PasswordChanged(DateTimeOffset at) => (
        "Mật khẩu tài khoản vừa được đổi",
        Layout("Mật khẩu vừa được đổi",
            $"""
            <p>Mật khẩu tài khoản JP Center của bạn vừa được đổi lúc {E(at.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm dd/MM/yyyy"))}.</p>
            <p>Mọi thiết bị khác đã bị đăng xuất. Nếu không phải bạn thực hiện, hãy liên hệ quản trị viên ngay.</p>
            """));

    public static (string Subject, string Html) Invitation(string fullName, string roles, string link) => (
        "Mời kích hoạt tài khoản JP Center",
        Layout("Chào mừng bạn đến với JP Center",
            $"""
            <p>Xin chào {E(fullName)},</p>
            <p>Bạn được cấp tài khoản với vai trò: <b>{E(roles)}</b>.</p>
            <p><a href="{E(link)}" style="background:#b91c1c;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none">Đặt mật khẩu & kích hoạt</a></p>
            <p>Link có hiệu lực <b>72 giờ</b> và chỉ dùng được 1 lần.</p>
            """));

    public static (string Subject, string Html) LockoutWarning(int times) => (
        "Cảnh báo: tài khoản bị khóa tạm nhiều lần",
        Layout("Có người đang thử đăng nhập vào tài khoản của bạn",
            $"""
            <p>Tài khoản của bạn đã bị khóa tạm <b>{times} lần</b> trong 24 giờ qua do nhập sai mật khẩu liên tiếp.</p>
            <p>Nếu không phải bạn, hãy đổi mật khẩu ngay và báo quản trị viên.</p>
            """));

    public static (string Subject, string Html) TemporaryPassword(string displayName, string username, string password, string loginLink) => (
        "Tài khoản Cổng học viên / phụ huynh JP Center",
        Layout("Thông tin đăng nhập",
            $"""
            <p>Xin chào {E(displayName)},</p>
            <p>Tên đăng nhập: <b>{E(username)}</b><br/>Mật khẩu tạm: <b>{E(password)}</b></p>
            <p>Lần đăng nhập đầu tiên bạn sẽ được yêu cầu đổi mật khẩu. <a href="{E(loginLink)}">Đăng nhập</a></p>
            """));
}
