using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Trash;

public static class TrashErrors
{
    public static readonly Error NotFound = Error.NotFound("system.trash.not_found", "Không tìm thấy mục trong Thùng rác");
    public static readonly Error Expired = Error.Conflict("system.trash.expired", "Mục này đã quá hạn giữ trong Thùng rác, không khôi phục được");
    public static readonly Error NotRestorable = Error.Conflict("system.trash.not_restorable", "Loại dữ liệu này chưa hỗ trợ khôi phục");
    public static readonly Error SourceMissing = Error.Conflict("system.trash.source_missing", "Dữ liệu gốc không còn — đã bỏ mục khỏi Thùng rác");
    public static readonly Error KeepDaysInvalid = Error.Validation("system.trash.keep_days_invalid", "Số ngày giữ trong Thùng rác phải từ 1 đến 365");
}
