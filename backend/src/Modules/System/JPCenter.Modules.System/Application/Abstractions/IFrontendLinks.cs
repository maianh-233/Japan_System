namespace JPCenter.Modules.System.Application.Abstractions;

/// <summary>Đường dẫn trang frontend gửi trong email (cấu hình <c>App:FrontendUrl</c>).</summary>
public interface IFrontendLinks
{
    /// <summary>Trang kích hoạt tài khoản được mời (AUTH-05).</summary>
    string Activation(string token);

    string Login();
}
