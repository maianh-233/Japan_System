namespace JPCenter.Modules.Finance.Contracts;

/// <summary>
/// TC-13.E2 — bảng lương GV đã chốt thì không xác nhận / bỏ xác nhận buổi của tháng đó (phải mở khóa trước).
/// Academic hỏi trước khi đổi xác nhận buổi.
/// </summary>
public interface IPayrollLocks
{
    /// <summary>Bảng lương GV tháng chứa <paramref name="date"/> của chi nhánh (chi nhánh của LỚP) đang khóa</summary>
    Task<bool> IsTeacherPayrollLockedAsync(Guid branchId, DateOnly date, CancellationToken ct);
}
