using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Requests;

public static class RequestErrors
{
    public static readonly Error NotPending = Error.Conflict("academic.request.not_pending", "Đơn đã được xử lý");
    public static readonly Error RejectReasonRequired = Error.Validation("academic.request.reject_reason_required", "Cần ghi lý do từ chối");
    public static readonly Error SessionGone = Error.Conflict("academic.request.session_gone", "Buổi học hoặc lớp học liên kết không còn tồn tại");
    public static readonly Error InviteClosed = Error.Conflict("academic.invite.closed", "Lời mời đã hết hiệu lực");
    public static readonly Error InviteInvalid = Error.Validation("academic.invite.invalid", "Lời mời không hợp lệ (mời chính GV vắng hoặc hạn trả lời đã qua)");

    // DT-16 — đơn hủy buổi của GV
    public static readonly Error CancelSessionStarted = Error.Conflict("academic.session.already_started", "Buổi đã bắt đầu — gọi giáo vụ để báo sự cố");
    public static readonly Error CancelReasonRequired = Error.Validation("academic.cancel_request.reason_required", "Nhập loại lý do và chi tiết (tối thiểu 10 ký tự)");
    public static readonly Error CancelDuplicate = Error.Conflict("academic.cancel_request.duplicate", "Bạn đã có đơn hủy buổi này đang chờ duyệt");

    // DT-18 — đơn xin nghỉ của HV / PH
    public static readonly Error LeaveTooLate = Error.Conflict("academic.leave_request.too_late", "Buổi này đã bắt đầu — liên hệ giáo vụ để báo vắng");
    public static readonly Error LeaveReasonRequired = Error.Validation("academic.leave_request.reason_required", "Nhập loại lý do và chi tiết (tối thiểu 5 ký tự)");
    public static readonly Error LeaveDuplicate = Error.Conflict("academic.leave_request.duplicate", "Đã có đơn xin nghỉ cho buổi này");
    public static readonly Error MakeupInvalid = Error.Validation("academic.leave_request.makeup_invalid",
        "Buổi học bù phải của lớp khác cùng khóa, cùng bài, chưa diễn ra và trong 3 tuần tới");
    public static readonly Error MakeupFull = Error.Conflict("academic.leave_request.makeup_full", "Lớp học bù đã đủ chỗ");
    public static readonly Error MakeupClash = Error.Conflict("academic.leave_request.makeup_clash", "Buổi học bù trùng giờ lớp khác đang học");
}
