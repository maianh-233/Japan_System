using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Students.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Students.Application.Enrollments.GetEnrollments;

/// <summary>Ghi danh (của 1 lớp / 1 HV / mọi ghi danh trong phạm vi) — tab Học viên của lớp, tab Lớp học của hồ sơ HV</summary>
public sealed record GetEnrollmentsQuery(Guid? ClassId, Guid? StudentId) : IQuery<IReadOnlyList<EnrollmentDto>>;

public sealed class GetEnrollmentsHandler(EnrollmentReader reader) : IQueryHandler<GetEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>
{
    public async Task<Result<IReadOnlyList<EnrollmentDto>>> Handle(GetEnrollmentsQuery q, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(q.ClassId, q.StudentId, ct));
}
