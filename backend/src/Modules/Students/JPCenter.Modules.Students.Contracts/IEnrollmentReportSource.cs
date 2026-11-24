using JPCenter.Modules.Students.Contracts.Dtos;

namespace JPCenter.Modules.Students.Contracts;

/// <summary>Số liệu ghi danh cho Reporting (user story 17, ADR 0004). <c>branchIds</c> null = mọi chi nhánh.</summary>
public interface IEnrollmentReportSource
{
    /// <summary>Ghi danh đang học / bảo lưu + mọi ghi danh có ngày ghi danh từ <paramref name="enrolledFrom"/></summary>
    Task<IReadOnlyList<EnrollmentFact>> EnrollmentsAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly enrolledFrom, CancellationToken ct);
}
