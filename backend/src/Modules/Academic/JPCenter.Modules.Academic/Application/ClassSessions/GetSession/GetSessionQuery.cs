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
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.ClassSessions.GetSession;

/// <summary>Chi tiết 1 buổi (ngoài phạm vi → 404)</summary>
public sealed record GetSessionQuery(Guid Id) : IQuery<SessionDto>;

public sealed class GetSessionHandler(SessionReader reader) : IQueryHandler<GetSessionQuery, SessionDto>
{
    public async Task<Result<SessionDto>> Handle(GetSessionQuery q, CancellationToken ct) =>
        await reader.FindAsync(q.Id, AcademicAccess.Sessions, ct) is { } dto ? dto : SessionErrors.NotFound;
}
