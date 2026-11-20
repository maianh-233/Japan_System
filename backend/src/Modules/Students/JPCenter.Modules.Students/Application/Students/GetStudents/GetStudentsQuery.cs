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
namespace JPCenter.Modules.Students.Application.Students.GetStudents;

/// <summary>HV-01 — học viên trong phạm vi xem (tìm / lọc ở frontend: vài trăm HV / chi nhánh)</summary>
public sealed record GetStudentsQuery(bool IncludeHidden, Guid? BranchId) : IQuery<IReadOnlyList<StudentDto>>;

public sealed class GetStudentsHandler(StudentReader reader) : IQueryHandler<GetStudentsQuery, IReadOnlyList<StudentDto>>
{
    public async Task<Result<IReadOnlyList<StudentDto>>> Handle(GetStudentsQuery q, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(q.IncludeHidden, q.BranchId, ct));
}
