namespace JPCenter.Modules.Academic.Contracts.Dtos.Reports;

/// <summary>Đơn đào tạo đang chờ duyệt (BC-02 Giáo vụ).</summary>
/// <param name="Kind">cancel (GV xin hủy buổi) · leave (HV / PH xin nghỉ)</param>
/// <param name="Who">Người gửi</param>
/// <param name="SessionStart">Giờ bắt đầu buổi học liên quan (giờ VN); null khi buổi không còn</param>
public sealed record PendingClassRequest(Guid Id, string Kind, string Who, Guid BranchId, string? ClassCode, DateTimeOffset RequestedAt,
    DateTime? SessionStart);
