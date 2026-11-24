using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>Lỗi thư viện tài liệu (user story 16)</summary>
public static class LibraryDocumentErrors
{
    /// <summary>TV-05.E1 — cả khi tài liệu ẩn / không đúng đối tượng / khác chi nhánh (không lộ là có)</summary>
    public static readonly Error NotFound = Error.NotFound("library.document.not_found", "Không tìm thấy tài liệu");
    public static readonly Error TitleRequired = Error.Validation("library.document.title_required", "Chưa nhập tên tài liệu");
    public static readonly Error TitleTooLong = Error.Validation("library.document.title_too_long", "Tên tài liệu tối đa 200 ký tự");
    public static readonly Error AudienceRequired = Error.Validation("library.document.audience_required", "Chọn đối tượng được xem");
    public static readonly Error InvalidLink = Error.Validation("library.document.invalid_link", "Liên kết phải bắt đầu bằng http:// hoặc https://");
    public static readonly Error FileRequired = Error.Validation("library.document.file_required", "Chưa tải tệp tài liệu");
    public static readonly Error FileTypeMismatch = Error.Validation("library.document.file_type_mismatch", "Tệp không đúng dạng tài liệu đã chọn");
    public static readonly Error InvalidMeta = Error.Validation("library.document.invalid_meta", "Năm xuất bản, số trang hoặc số phút không hợp lệ");
    public static readonly Error ViewNotFound = Error.NotFound("library.view.not_found", "Không tìm thấy lượt xem");
}
