using JPCenter.Modules.Academic.Contracts.Dtos.Reports;

namespace JPCenter.Modules.Academic.Contracts;

/// <summary>Số liệu đào tạo cho Reporting (user story 17, ADR 0004). <c>branchIds</c> null = mọi chi nhánh — người gọi đã lọc theo quyền.</summary>
public interface IAcademicReportSource
{
    /// <summary>Lớp đang học + đang tuyển sinh</summary>
    Task<IReadOnlyList<ClassFact>> OpenClassesAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct);

    /// <summary>Lượt điểm danh theo chi nhánh × tháng của buổi chưa hủy có ngày trong [from, to]</summary>
    Task<IReadOnlyList<AttendanceMonthFact>> AttendanceByMonthAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Mọi phòng trong chi nhánh + số phút có lớp trong [from, to] (phòng của buổi, không có thì phòng của lớp)</summary>
    Task<IReadOnlyList<RoomUsageFact>> RoomUsageAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Buổi đã dạy chưa xác nhận (từ <paramref name="unconfirmedFrom"/>) và chưa có báo cáo buổi (từ <paramref name="missingLogFrom"/>)</summary>
    Task<SessionAlertCounts> SessionAlertsAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly unconfirmedFrom, DateOnly missingLogFrom, CancellationToken ct);

    /// <summary>Buổi chưa điểm danh có ngày trong [from, to]</summary>
    Task<IReadOnlyList<SessionWithoutAttendance>> SessionsWithoutAttendanceAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from, DateOnly to,
        CancellationToken ct);

    /// <summary>Đơn hủy buổi + đơn xin nghỉ đang chờ duyệt</summary>
    Task<IReadOnlyList<PendingClassRequest>> PendingRequestsAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct);
}
