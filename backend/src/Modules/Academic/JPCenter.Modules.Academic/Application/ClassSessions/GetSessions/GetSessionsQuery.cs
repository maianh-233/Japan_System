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
namespace JPCenter.Modules.Academic.Application.ClassSessions.GetSessions;

/// <summary>DT-20 — buổi học theo khoảng ngày / chi nhánh / lớp / GV / phòng (bảng buổi trong ngày, thời khóa biểu)</summary>
public sealed record GetSessionsQuery(SessionFilter Filter) : IQuery<IReadOnlyList<SessionDto>>;

public sealed class GetSessionsValidator : AbstractValidator<GetSessionsQuery>
{
    public GetSessionsValidator() =>
        RuleFor(x => x.Filter).Must(f => f.ClassId is not null || (f.From is { } a && f.To is { } b && b >= a && b.DayNumber - a.DayNumber <= 92))
            .WithMessage("Chọn lớp, hoặc khoảng ngày (tối đa 3 tháng)");
}

public sealed class GetSessionsHandler(SessionReader reader) : IQueryHandler<GetSessionsQuery, IReadOnlyList<SessionDto>>
{
    public async Task<Result<IReadOnlyList<SessionDto>>> Handle(GetSessionsQuery q, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(q.Filter, AcademicAccess.Sessions, ct));
}
