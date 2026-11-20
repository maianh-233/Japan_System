using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Promotions;

public static class PromotionErrors
{
    public static readonly Error NotFound = Error.NotFound("academic.promotion.not_found", "Không tìm thấy đề xuất lên lớp");
    public static readonly Error Closed = Error.Conflict("academic.promotion.closed", "Đề xuất lên lớp đã chốt hoặc đã từ chối");
    public static readonly Error ToClassRequired = Error.Validation("academic.promotion.to_class_required", "Chưa chọn lớp lên");
    public static readonly Error InvalidHoldUntil = Error.Validation("academic.promotion.invalid_hold_until", "Hạn giữ chỗ phải từ hôm nay trở đi");
    public static readonly Error SameClass = Error.Validation("academic.promotion.same_class", "Lớp lên phải khác lớp đang học");
    public static readonly Error Duplicate = Error.Conflict("academic.promotion.duplicate", "Học viên đã có trong danh sách lên lớp của lớp này");
    public static readonly Error DeclineReasonRequired = Error.Validation("academic.promotion.decline_reason_required", "Cần ghi lý do từ chối");
    public static readonly Error NoteRequired = Error.Validation("academic.promotion.note_required", "Chưa nhập nội dung tư vấn");
}
