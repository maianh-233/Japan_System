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
namespace JPCenter.Modules.Students.Application.Students.GetStudent;

/// <summary>HV-02 — 1 hồ sơ (kể cả đã ẩn — HV-02.E3); ngoài phạm vi → 404</summary>
public sealed record GetStudentQuery(Guid Id) : IQuery<StudentDto>;

public sealed class GetStudentHandler(StudentReader reader) : IQueryHandler<GetStudentQuery, StudentDto>
{
    public async Task<Result<StudentDto>> Handle(GetStudentQuery q, CancellationToken ct) =>
        await reader.FindAsync(q.Id, ct) is { } dto ? dto : StudentErrors.NotFound;
}
