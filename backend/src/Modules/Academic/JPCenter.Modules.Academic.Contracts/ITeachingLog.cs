using JPCenter.Modules.Academic.Contracts.Dtos;

namespace JPCenter.Modules.Academic.Contracts;

/// <summary>Buổi đã dạy / bị hủy của GV / trợ giảng trong khoảng ngày — Finance tính lương GV (TC-13), cổng GV xem lương tạm tính.</summary>
public interface ITeachingLog
{
    /// <param name="teacherId">Chỉ của 1 người (cổng GV); null = mọi người</param>
    Task<IReadOnlyList<TaughtSession>> SessionsAsync(DateOnly from, DateOnly to, Guid? teacherId, CancellationToken ct);
}
