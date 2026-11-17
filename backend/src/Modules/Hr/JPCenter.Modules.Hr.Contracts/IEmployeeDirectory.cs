using JPCenter.Modules.Hr.Contracts.Dtos;

namespace JPCenter.Modules.Hr.Contracts;

/// <summary>Tra cứu nhân viên — cách DUY NHẤT module khác đọc dữ liệu nhân viên (CLAUDE.md mục 3).</summary>
public interface IEmployeeDirectory
{
    Task<EmployeeSummary?> FindAsync(Guid employeeId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, EmployeeSummary>> FindManyAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken ct);

    /// <summary>Nhân viên chưa nghỉ việc — System trừ đi người đã có tài khoản để ra "N nhân viên chưa có tài khoản" (PQ-08).</summary>
    Task<IReadOnlyList<EmployeeSummary>> ListActiveAsync(CancellationToken ct);

    /// <summary>Số nhân sự / giáo viên chưa nghỉ theo chi nhánh chính (CN-01, CN-06.E2). Chi nhánh không có ai → không có trong kết quả.</summary>
    Task<IReadOnlyDictionary<Guid, BranchHeadcount>> HeadcountByBranchAsync(CancellationToken ct);
}
