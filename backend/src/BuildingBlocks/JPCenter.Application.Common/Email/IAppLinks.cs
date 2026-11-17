namespace JPCenter.Application.Common.Email;

/// <summary>Đường dẫn trang frontend để gắn vào email / thông báo (cấu hình <c>App:FrontendUrl</c>) — dùng ở mọi module.</summary>
public interface IAppLinks
{
    /// <summary>vd <c>Page("/admin/hr/employees?open=" + id)</c> → http://localhost:5173/admin/hr/employees?open=...</summary>
    string Page(string path);
}
