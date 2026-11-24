using JPCenter.Modules.Crm.Contracts.Dtos;

namespace JPCenter.Modules.Crm.Contracts;

/// <summary>
/// Phạm vi <c>own</c> của trang Học viên (PQ-10): HV có lead liên kết do Sale này phụ trách. Lead đã ẩn vẫn tính.
/// </summary>
public interface ILeadOwnership
{
    /// <summary>Id học viên (Students.Student) liên kết với lead có <c>assignee_id</c> = <paramref name="employeeId"/></summary>
    Task<IReadOnlyList<Guid>> StudentIdsOfSaleAsync(Guid employeeId, CancellationToken ct);

    /// <summary>Sale phụ trách lead liên kết với HV (lead đã ẩn vẫn tính) — Finance ghi nhận doanh số (TC-04.E5)</summary>
    Task<StudentLeadSales> SalesOfStudentAsync(Guid studentId, CancellationToken ct);
}
