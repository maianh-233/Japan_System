namespace JPCenter.SharedKernel.Domain;

/// <summary>
/// Đánh dấu entity xóa mềm. Cột is_deleted / deleted_at / deleted_by là shadow property,
/// interceptor tự điền — entity không tự quản lý.
/// </summary>
public interface ISoftDeletable;
