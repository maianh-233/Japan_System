using JPCenter.Modules.Hr.Contracts.Dtos;

namespace JPCenter.Modules.Hr.Contracts;

/// <summary>Tra cứu nhân viên — cách DUY NHẤT module khác đọc dữ liệu nhân viên (CLAUDE.md mục 3).</summary>
public interface IEmployeeDirectory
{
    Task<EmployeeSummary?> FindAsync(Guid employeeId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, EmployeeSummary>> FindManyAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken ct);

    /// <summary>Nhân viên chưa nghỉ việc mà chưa có tài khoản (PQ-08: "N nhân viên chưa có tài khoản").</summary>
    Task<IReadOnlyList<EmployeeSummary>> ListWithoutAccountAsync(CancellationToken ct);
}
