namespace JPCenter.SharedKernel.Primitives;

/// <summary>Nhóm thao tác ghi nhật ký (audit_logs.category) — mọi module dùng chung.</summary>
public enum AuditCategory
{
    /// <summary>Đăng nhập / bảo mật</summary>
    Auth,
    /// <summary>Phân quyền</summary>
    Permission,
    /// <summary>Thêm / sửa / xóa dữ liệu</summary>
    Data,
    /// <summary>Xuất file</summary>
    Export,
    /// <summary>Tài chính</summary>
    Finance,
    /// <summary>Cài đặt</summary>
    Setting,
    /// <summary>An ninh: vượt rate limit lặp lại, token bị dùng lại, dò quyền</summary>
    Security,
}
