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
namespace JPCenter.Modules.Academic.Application.Classes.GetClass;

/// <summary>DT-01 — chi tiết 1 lớp (ngoài phạm vi → 404)</summary>
public sealed record GetClassQuery(Guid Id) : IQuery<ClassDto>;

public sealed class GetClassHandler(ClassReader reader) : IQueryHandler<GetClassQuery, ClassDto>
{
    public async Task<Result<ClassDto>> Handle(GetClassQuery q, CancellationToken ct) =>
        await reader.FindAsync(q.Id, ct) is { } dto ? dto : ClassErrors.NotFound;
}
