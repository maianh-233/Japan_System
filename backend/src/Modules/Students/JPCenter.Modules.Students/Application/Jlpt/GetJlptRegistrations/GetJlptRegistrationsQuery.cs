using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Application.Jlpt.GetJlptRegistrations;

/// <summary>HV-16 — danh sách đăng ký thi theo kỳ / HV / trạng thái, phạm vi theo chi nhánh của HV</summary>
public sealed record GetJlptRegistrationsQuery(Guid? SessionId, Guid? StudentId, JlptRegistrationStatus? Status)
    : IQuery<IReadOnlyList<JlptRegistrationDto>>;

public sealed class GetJlptRegistrationsHandler(JlptReader reader) : IQueryHandler<GetJlptRegistrationsQuery, IReadOnlyList<JlptRegistrationDto>>
{
    public async Task<Result<IReadOnlyList<JlptRegistrationDto>>> Handle(GetJlptRegistrationsQuery q, CancellationToken ct)
    {
        var query = reader.Scoped(PermissionAction.View);
        if (q.SessionId is { } sid) query = query.Where(r => r.SessionId == new JlptSessionId(sid));
        if (q.StudentId is { } st) query = query.Where(r => r.StudentId == new StudentId(st));
        if (q.Status is { } status) query = query.Where(r => r.Status == status);
        return Result.Success(await reader.ToDtosAsync(query, ct));
    }
}
