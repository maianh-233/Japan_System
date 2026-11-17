using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Application.Common.Files;

/// <summary>Lỗi khi tải file lên — dùng chung mọi module</summary>
public static class FileErrors
{
    public static readonly Error Empty = Error.Validation("file.empty", "Tệp rỗng");

    public static readonly Error NameRequired = Error.Validation("file.name_required", "Thiếu tên tệp");

    public static Error TooLarge(long maxBytes) =>
        Error.Validation("file.too_large", $"Tệp vượt quá {maxBytes / 1024 / 1024} MB");

    public static Error TypeNotAllowed(IEnumerable<string> allowed) =>
        Error.Validation("file.type_not_allowed", $"Chỉ nhận tệp: {string.Join(", ", allowed)}");

    /// <summary>Đuôi file không khớp nội dung thật (đổi đuôi để lách)</summary>
    public static readonly Error ContentMismatch =
        Error.Validation("file.content_mismatch", "Nội dung tệp không đúng định dạng của đuôi tệp");

    public static readonly Error InvalidFolder =
        Error.Validation("file.invalid_folder", "Thư mục lưu không hợp lệ");
}
