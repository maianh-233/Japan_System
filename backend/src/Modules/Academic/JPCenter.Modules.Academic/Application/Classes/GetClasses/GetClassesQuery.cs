using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.Classes.GetClasses;

/// <summary>DT-01 — lớp trong phạm vi xem</summary>
public sealed record GetClassesQuery(ClassFilter Filter) : IQuery<IReadOnlyList<ClassDto>>;

public sealed class GetClassesHandler(ClassReader reader) : IQueryHandler<GetClassesQuery, IReadOnlyList<ClassDto>>
{
    public async Task<Result<IReadOnlyList<ClassDto>>> Handle(GetClassesQuery q, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(q.Filter, ct));
}
