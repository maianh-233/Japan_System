using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Portals.GetTeachingSessions;

/// <summary>DT-20.E1 — cổng GV: buổi mình dạy / dạy thay / trợ giảng trong khoảng ngày (≤ 3 tháng). Không có học phí.</summary>
public sealed record GetTeachingSessionsQuery(DateOnly From, DateOnly To) : IQuery<IReadOnlyList<SessionDto>>;

/// <summary>Cổng GV: chi tiết 1 buổi mình tham gia</summary>
public sealed record GetTeachingSessionQuery(Guid Id) : IQuery<SessionDto>;

public sealed class GetTeachingSessionsValidator : AbstractValidator<GetTeachingSessionsQuery>
{
    public GetTeachingSessionsValidator() =>
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("Ngày kết thúc phải sau ngày bắt đầu")
            .Must((q, to) => to.DayNumber - q.From.DayNumber <= 93).WithMessage("Chỉ xem tối đa 3 tháng");
}

public sealed class GetTeachingSessionsHandler(SessionReader reader, PortalOwner owner)
    : IQueryHandler<GetTeachingSessionsQuery, IReadOnlyList<SessionDto>>, IQueryHandler<GetTeachingSessionQuery, SessionDto>
{
    public async Task<Result<IReadOnlyList<SessionDto>>> Handle(GetTeachingSessionsQuery q, CancellationToken ct) =>
        owner.TeacherId is { } me ? Result.Success(await reader.ListForTeacherAsync(me, q.From, q.To, ct)) : Result.Success<IReadOnlyList<SessionDto>>([]);

    public async Task<Result<SessionDto>> Handle(GetTeachingSessionQuery q, CancellationToken ct) =>
        owner.TeacherId is { } me && await reader.FindForTeacherAsync(q.Id, me, ct) is { } dto ? dto : SessionErrors.NotFound;
}
