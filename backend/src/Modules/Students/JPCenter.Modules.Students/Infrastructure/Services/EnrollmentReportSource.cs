using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Students.Contracts.Dtos;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IEnrollmentReportSource"/> — chỉ chiếu 5 cột, không kéo hồ sơ HV.</summary>
internal sealed class EnrollmentReportSource(StudentsDbContext db) : IEnrollmentReportSource
{
    public async Task<IReadOnlyList<EnrollmentFact>> EnrollmentsAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly enrolledFrom, CancellationToken ct)
    {
        var query = db.Enrollments.AsNoTracking()
            .Where(e => e.Status == EnrollmentStatus.Studying || e.Status == EnrollmentStatus.Reserved || e.EnrolledAt >= enrolledFrom);
        if (branchIds is not null)
        {
            var ids = branchIds.Select(b => new BranchId(b)).ToList();
            query = query.Where(e => ids.Contains(e.BranchId));
        }
        var rows = await query.Select(e => new { e.StudentId, e.ClassId, e.BranchId, e.Status, e.EnrolledAt }).ToListAsync(ct);
        return rows.Select(e => new EnrollmentFact(e.StudentId.Value, e.ClassId, e.BranchId.Value,
            SnakeCaseEnumConverter<EnrollmentStatus>.ToText(e.Status), e.EnrolledAt)).ToList();
    }
}
