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
namespace JPCenter.Modules.Academic.Application.ClassSessions.GetClassSessions;

/// <summary>Tab Buổi học của chi tiết lớp — mọi buổi của 1 lớp (quyền xem lớp)</summary>
public sealed record GetClassSessionsQuery(Guid ClassId) : IQuery<IReadOnlyList<SessionDto>>;

public sealed class GetClassSessionsHandler(ClassReader classes, SessionReader reader) : IQueryHandler<GetClassSessionsQuery, IReadOnlyList<SessionDto>>
{
    public async Task<Result<IReadOnlyList<SessionDto>>> Handle(GetClassSessionsQuery q, CancellationToken ct)
    {
        var cls = await classes.FindAsync(q.ClassId, ct);
        if (cls is null) return ClassErrors.NotFound;
        // Đã thấy lớp → thấy mọi buổi của lớp (không lọc own theo từng buổi)
        return Result.Success(await reader.ListAsync(new SessionFilter(null, null, null, q.ClassId, null, null), AcademicAccess.Classes, ct));
    }
}
