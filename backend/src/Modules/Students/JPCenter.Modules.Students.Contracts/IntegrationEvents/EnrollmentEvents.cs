using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.Students.Contracts.IntegrationEvents;

/// <summary>
/// Ghi danh mới (HV-06, hoặc ghi danh đích khi chuyển lớp HV-09) → Finance lập hóa đơn: <paramref name="Amount"/> học phí,
/// <paramref name="Discount"/> giảm trừ (chuyển lớp = khấu trừ tiền đã đóng chưa học), hạn đóng <paramref name="DueDate"/>.
/// </summary>
public sealed record StudentEnrolled(Guid EnrollmentId, Guid StudentId, Guid ClassId, Guid BranchId, decimal Amount, decimal Discount,
    DateOnly? DueDate, Guid? TransferredFromId) : IIntegrationEvent;

/// <summary>
/// Ghi danh kết thúc → Finance xử lý phần học phí còn lại. <paramref name="Status"/>: completed · dropped · transferred
/// (dropped / transferred → hủy công nợ còn lại; tiền đã thu giữ nguyên để hoàn / khấu trừ).
/// </summary>
public sealed record EnrollmentEnded(Guid EnrollmentId, Guid StudentId, Guid ClassId, string Status, DateOnly EndedAt, string? Reason)
    : IIntegrationEvent;
