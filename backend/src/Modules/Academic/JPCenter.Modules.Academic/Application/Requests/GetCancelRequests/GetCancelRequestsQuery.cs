using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.GetCancelRequests;

/// <summary>DT-16 — Đơn chờ xử lý (giáo vụ): đơn hủy buổi theo trạng thái / chi nhánh. Đơn đã thu hồi không hiện.</summary>
public sealed record GetCancelRequestsQuery(RequestStatus? Status, Guid? BranchId) : IQuery<IReadOnlyList<CancelRequestDto>>;

/// <summary>DT-16 — cổng GV: đơn hủy buổi mình đã gửi</summary>
public sealed record GetMyCancelRequestsQuery : IQuery<IReadOnlyList<CancelRequestDto>>;

public sealed class GetCancelRequestsHandler(IAcademicDbContext db, RequestReader reader, TeacherSessions teacher)
    : IQueryHandler<GetCancelRequestsQuery, IReadOnlyList<CancelRequestDto>>, IQueryHandler<GetMyCancelRequestsQuery, IReadOnlyList<CancelRequestDto>>
{
    public async Task<Result<IReadOnlyList<CancelRequestDto>>> Handle(GetCancelRequestsQuery q, CancellationToken ct)
    {
        var query = db.SessionCancelRequests.AsNoTracking().Where(r => r.Status != RequestStatus.Withdrawn);
        if (reader.Branches(PermissionAction.View) is { } branches) query = query.Where(r => branches.Contains(r.BranchId));
        if (q.Status is { } st) query = query.Where(r => r.Status == st);
        if (q.BranchId is { } b) query = query.Where(r => r.BranchId == new BranchId(b));
        return Result.Success(await reader.CancelDtosAsync(await query.ToListAsync(ct), ct));
    }

    public async Task<Result<IReadOnlyList<CancelRequestDto>>> Handle(GetMyCancelRequestsQuery q, CancellationToken ct)
    {
        var list = await db.SessionCancelRequests.AsNoTracking()
            .Where(r => r.TeacherId == teacher.Me && r.Status != RequestStatus.Withdrawn).ToListAsync(ct);
        return Result.Success(await reader.CancelDtosAsync(list, ct));
    }
}
