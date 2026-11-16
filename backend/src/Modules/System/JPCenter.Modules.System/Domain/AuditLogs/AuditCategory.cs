namespace JPCenter.Modules.System.Domain.AuditLogs;

/// <summary>Nhóm thao tác ghi nhật ký</summary>
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
}
