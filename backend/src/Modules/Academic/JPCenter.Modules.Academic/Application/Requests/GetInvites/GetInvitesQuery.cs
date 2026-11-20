using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.GetInvites;

/// <summary>DT-16 — giáo vụ: lời mời dạy thay theo buổi / trạng thái / chi nhánh</summary>
public sealed record GetInvitesQuery(Guid? SessionId, InviteStatus? Status, Guid? BranchId) : IQuery<IReadOnlyList<InviteDto>>;

/// <summary>DT-16 — cổng GV: lời mời dạy thay gửi tới mình</summary>
public sealed record GetMyInvitesQuery : IQuery<IReadOnlyList<InviteDto>>;

public sealed class GetInvitesHandler(IAcademicDbContext db, RequestReader reader, TeacherSessions teacher)
    : IQueryHandler<GetInvitesQuery, IReadOnlyList<InviteDto>>, IQueryHandler<GetMyInvitesQuery, IReadOnlyList<InviteDto>>
{
    public async Task<Result<IReadOnlyList<InviteDto>>> Handle(GetInvitesQuery q, CancellationToken ct)
    {
        var query = db.SubstituteInvites.AsNoTracking();
        if (reader.Branches(PermissionAction.View) is { } branches) query = query.Where(i => branches.Contains(i.BranchId));
        if (q.SessionId is { } sid) query = query.Where(i => i.SessionId == new ClassSessionId(sid));
        if (q.Status is { } st) query = query.Where(i => i.Status == st);
        if (q.BranchId is { } b) query = query.Where(i => i.BranchId == new BranchId(b));
        return Result.Success(await reader.InviteDtosAsync(await query.ToListAsync(ct), ct));
    }

    public async Task<Result<IReadOnlyList<InviteDto>>> Handle(GetMyInvitesQuery q, CancellationToken ct) =>
        Result.Success(await reader.InviteDtosAsync(await db.SubstituteInvites.AsNoTracking().Where(i => i.ToTeacherId == teacher.Me).ToListAsync(ct), ct));
}
