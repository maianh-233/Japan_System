using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>Lỗi đơn từ (user story 13, DN-xx) — mã trùng tài liệu.</summary>
public static class HrRequestErrors
{
    public static readonly Error NotFound = Error.NotFound("hr.request.not_found", "Không tìm thấy đơn");
    /// <summary>DN-01.E1</summary>
    public static readonly Error InvalidDates = Error.Validation("hr.request.invalid_dates", "Ngày kết thúc phải từ ngày bắt đầu trở đi");
    /// <summary>DN-01.E2, DN-02.E5</summary>
    public static readonly Error LeaveExceeded = Error.Conflict("hr.request.leave_exceeded", "Không đủ ngày phép năm — bỏ \"có lương\" để nghỉ không lương");
    /// <summary>DN-01.E3</summary>
    public static readonly Error NoWorkdays = Error.Validation("hr.request.no_workdays", "Khoảng nghỉ không có ngày làm việc nào");
    /// <summary>DN-01.E4</summary>
    public static readonly Error Overlap = Error.Conflict("hr.request.overlap", "Đã có đơn nghỉ / công tác trùng thời gian");
    /// <summary>DN-01.E5</summary>
    public static readonly Error AmountRequired = Error.Validation("hr.request.amount_required", "Nhập số tiền hợp lệ");
    public static readonly Error TitleRequired = Error.Validation("hr.request.title_required", "Nhập tiêu đề đề xuất");
    public static readonly Error ReasonMissing = Error.Validation("hr.request.reason_missing", "Nhập lý do");
    /// <summary>DN-01.E6</summary>
    public static readonly Error InvalidSwap = Error.Validation("hr.request.invalid_swap", "Ca đổi không hợp lệ (phải là ca sắp tới của bạn và của đồng nghiệp cùng chi nhánh)");
    /// <summary>DN-01.E7</summary>
    public static readonly Error NotStaff = Error.Forbidden("hr.request.not_staff", "Giáo viên / trợ giảng gửi đơn ở Cổng giáo viên");
    /// <summary>DN-01.E8</summary>
    public static readonly Error TooOld = Error.Validation("hr.request.too_old", "Chỉ bổ sung đơn cho ngày trong 30 ngày gần đây");
    /// <summary>DN-02.E1</summary>
    public static readonly Error NotYourTurn = Error.Forbidden("hr.request.not_your_turn", "Chưa đến lượt bạn duyệt đơn này");
    /// <summary>DN-02.E2</summary>
    public static readonly Error NotPending = Error.Conflict("hr.request.not_pending", "Đơn đã được xử lý");
    /// <summary>DN-02.E3</summary>
    public static readonly Error SelfApprove = Error.Forbidden("hr.request.self_approve", "Không tự duyệt đơn của chính mình");
    /// <summary>DN-02.E4</summary>
    public static readonly Error ReasonRequired = Error.Validation("hr.request.reason_required", "Từ chối cần ghi lý do");
    /// <summary>DN-02.E6</summary>
    public static readonly Error SwapStale = Error.Conflict("hr.request.swap_stale", "Ca trong đơn đã thay đổi / đã qua — không duyệt được, hãy từ chối");
    /// <summary>DN-03</summary>
    public static readonly Error OnlyOwner = Error.Forbidden("hr.request.only_owner", "Chỉ người gửi được hủy đơn");
}
