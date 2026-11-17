namespace JPCenter.SharedKernel.Primitives;

/// <summary>Loại lỗi → API đổi sang HTTP status.</summary>
public enum ErrorType
{
    /// <summary>400 — dữ liệu vào sai</summary>
    Validation,
    /// <summary>404 — không tồn tại / ngoài phạm vi quyền</summary>
    NotFound,
    /// <summary>409 — trùng, sai trạng thái, sửa đè</summary>
    Conflict,
    /// <summary>403 — không có quyền / bị chặn</summary>
    Forbidden,
    /// <summary>401 — chưa đăng nhập / sai thông tin đăng nhập / token hỏng</summary>
    Unauthorized,
    /// <summary>500 — lỗi hệ thống</summary>
    Failure,
}
