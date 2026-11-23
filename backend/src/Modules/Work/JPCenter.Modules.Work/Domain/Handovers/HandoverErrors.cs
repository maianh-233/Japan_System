using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Handovers;

/// <summary>Lỗi bàn giao ca (user story 14, BG-xx) — mã trùng tài liệu.</summary>
public static class HandoverErrors
{
    public static readonly Error NotFound = Error.NotFound("work.handover.not_found", "Không tìm thấy bàn giao");
    /// <summary>BG-01.E1</summary>
    public static readonly Error InvalidReceiver = Error.Validation("work.handover.invalid_receiver", "Người nhận phải là nhân viên làm theo ca khác, cùng chi nhánh");
    /// <summary>BG-01.E2</summary>
    public static readonly Error Duplicate = Error.Conflict("work.handover.duplicate", "Bạn đã bàn giao ca này hôm nay");
    /// <summary>BG-01.E3</summary>
    public static readonly Error NoteRequired = Error.Validation("work.handover.note_required", "Tiền đếm lệch với hệ thống — ghi chú lý do");
    /// <summary>BG-01.E4</summary>
    public static readonly Error LeadInvalid = Error.Validation("work.handover.lead_invalid", "Lead không thuộc chi nhánh hoặc đã chốt");
    public static readonly Error InvalidCash = Error.Validation("work.handover.invalid_cash", "Số tiền không hợp lệ");
    /// <summary>BG-02</summary>
    public static readonly Error NotReceiver = Error.Forbidden("work.handover.not_receiver", "Chỉ người nhận ca được xác nhận");
    public static readonly Error Closed = Error.Conflict("work.handover.closed", "Bàn giao đã được xác nhận");
    public static readonly Error NotReceived = Error.Conflict("work.handover.not_received", "Nhận ca trước rồi mới đánh dấu việc tồn");
    public static readonly Error ItemNotFound = Error.NotFound("work.handover.item_not_found", "Không tìm thấy việc tồn");
}
