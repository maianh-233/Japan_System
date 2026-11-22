using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Domain.TeacherDocuments;

/// <summary>Lỗi tài liệu GV (user story 12, ND-10)</summary>
public static class TeacherDocumentErrors
{
    public static readonly Error NotFound = Error.NotFound("library.teacher_doc.not_found", "Không tìm thấy tài liệu");
    public static readonly Error TitleRequired = Error.Validation("library.teacher_doc.title_required", "Nhập tiêu đề tài liệu");
    public static readonly Error TitleTooLong = Error.Validation("library.teacher_doc.too_long", "Tiêu đề tối đa 200 ký tự");
    public static readonly Error ContentRequired = Error.Validation("library.teacher_doc.content_required", "Chọn file hoặc dán link");
    public static readonly Error ClassRequired = Error.Validation("library.teacher_doc.class_required", "Chọn ít nhất 1 lớp");
    /// <summary>ND-10.E1</summary>
    public static readonly Error ClassNotAllowed = Error.Forbidden("library.teacher_doc.class_not_allowed", "Chỉ gửi tài liệu cho lớp mình dạy");
    public static readonly Error NotOwner = Error.Forbidden("library.teacher_doc.not_owner", "Chỉ người soạn mới sửa / xóa được tài liệu");
}
