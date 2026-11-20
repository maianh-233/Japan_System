using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.GetLeaveRequests;

/// <summary>DT-18 — Đơn chờ xử lý (giáo vụ): đơn xin nghỉ theo trạng thái / chi nhánh / lớp. Đơn đã thu hồi không hiện.</summary>
public sealed record GetLeaveRequestsQuery(RequestStatus? Status, Guid? BranchId, Guid? ClassId) : IQuery<IReadOnlyList<LeaveRequestDto>>;

/// <summary>DT-18 — cổng HV / PH: đơn xin nghỉ của mình / của các con (lọc 1 con bằng <paramref name="StudentId"/>)</summary>
public sealed record GetMyLeaveRequestsQuery(Guid? StudentId) : IQuery<IReadOnlyList<LeaveRequestDto>>;

/// <summary>DT-18 bước 2 — cổng HV / PH: buổi học bù chọn được cho 1 buổi xin nghỉ</summary>
public sealed record GetMakeupOptionsQuery(Guid SessionId, Guid? StudentId) : IQuery<IReadOnlyList<MakeupOptionDto>>;

public sealed class GetLeaveRequestsHandler(IAcademicDbContext db, RequestReader reader, PortalOwner owner, LeaveAccess leaves, MakeupChecker makeups)
    : IQueryHandler<GetLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>, IQueryHandler<GetMyLeaveRequestsQuery, IReadOnlyList<LeaveRequestDto>>,
        IQueryHandler<GetMakeupOptionsQuery, IReadOnlyList<MakeupOptionDto>>
{
    public async Task<Result<IReadOnlyList<LeaveRequestDto>>> Handle(GetLeaveRequestsQuery q, CancellationToken ct)
    {
        var query = db.LeaveRequests.AsNoTracking().Where(l => l.Status != RequestStatus.Withdrawn);
        if (reader.Branches(PermissionAction.View) is { } branches) query = query.Where(l => branches.Contains(l.BranchId));
        if (q.Status is { } st) query = query.Where(l => l.Status == st);
        if (q.BranchId is { } b) query = query.Where(l => l.BranchId == new BranchId(b));
        if (q.ClassId is { } c) query = query.Where(l => l.ClassId == new ClassId(c));
        return Result.Success(await reader.LeaveDtosAsync(await query.ToListAsync(ct), ct));
    }

    public async Task<Result<IReadOnlyList<LeaveRequestDto>>> Handle(GetMyLeaveRequestsQuery q, CancellationToken ct)
    {
        IReadOnlyList<Guid> students;
        if (q.StudentId is { } one)
        {
            var s = await owner.StudentAsync(one, ct);
            if (s.IsFailure) return s.Error;
            students = [s.Value];
        }
        else students = await owner.StudentsAsync(ct);

        var list = await db.LeaveRequests.AsNoTracking()
            .Where(l => students.Contains(l.StudentId) && l.Status != RequestStatus.Withdrawn).ToListAsync(ct);
        return Result.Success(await reader.LeaveDtosAsync(list, ct));
    }

    public async Task<Result<IReadOnlyList<MakeupOptionDto>>> Handle(GetMakeupOptionsQuery q, CancellationToken ct)
    {
        var found = await leaves.SessionAsync(q.StudentId, q.SessionId, ct);
        if (found.IsFailure) return found.Error;
        var (studentId, session, cls) = found.Value;
        return Result.Success(await makeups.OptionsAsync(session, cls, studentId, ct));
    }
}
