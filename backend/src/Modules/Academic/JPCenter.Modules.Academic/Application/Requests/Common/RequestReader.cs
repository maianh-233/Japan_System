using JPCenter.Application.Common.Time;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.Common;

/// <summary>
/// Đọc đơn hủy buổi / lời mời dạy thay / đơn xin nghỉ ra DTO (kèm mã lớp, buổi, tên người). Danh sách nhân viên lọc theo chi nhánh
/// của trang Đơn chờ xử lý (<c>academic.requests</c>); đơn đã thu hồi không hiện.
/// </summary>
public sealed class RequestReader(IAcademicDbContext db, AcademicAccess access, PeopleNames people, IEnrollmentLookup enrollments)
{
    /// <summary>Chi nhánh trang Đơn chờ xử lý được xem — null = mọi chi nhánh</summary>
    public List<BranchId>? Branches(PermissionAction action) => access.Branches(AcademicAccess.Requests, action)?.Select(b => new BranchId(b)).ToList();

    public bool InBranch(PermissionAction action, BranchId branch) => access.InBranch(AcademicAccess.Requests, action, branch.Value);

    public async Task<IReadOnlyList<CancelRequestDto>> CancelDtosAsync(IReadOnlyList<SessionCancelRequest> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var ctx = await ContextAsync(list.Select(x => x.SessionId), list.Select(x => x.ClassId), ct);
        var names = await people.OfAsync(list.SelectMany(x => new[] { (Guid?)x.TeacherId, x.DecidedBy }), ct);
        return list.OrderByDescending(x => x.RequestedAt).Select(x => new CancelRequestDto(
            x.Id.Value, x.SessionId.Value, x.ClassId.Value, ctx.Code(x.ClassId), x.BranchId.Value, ctx.Session(x.SessionId), x.TeacherId,
            names.GetValueOrDefault(x.TeacherId, "—"), x.ReasonType, x.Reason, x.ProposedMakeupAt, x.Status, x.RequestedAt, x.DecidedBy,
            x.DecidedBy is { } d ? names.GetValueOrDefault(d, "—") : null, x.DecidedAt, x.Note)).ToList();
    }

    public async Task<IReadOnlyList<InviteDto>> InviteDtosAsync(IReadOnlyList<SubstituteInvite> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var ctx = await ContextAsync(list.Select(x => x.SessionId), list.Select(x => x.ClassId), ct);
        var names = await people.OfAsync(list.SelectMany(x => new Guid?[] { x.FromTeacherId, x.ToTeacherId, x.InvitedBy }), ct);
        return list.OrderByDescending(x => x.InvitedAt).Select(x => new InviteDto(
            x.Id.Value, x.SessionId.Value, x.ClassId.Value, ctx.Code(x.ClassId), x.BranchId.Value, ctx.Session(x.SessionId), x.CancelRequestId?.Value,
            x.FromTeacherId, names.GetValueOrDefault(x.FromTeacherId, "—"), x.ToTeacherId, names.GetValueOrDefault(x.ToTeacherId, "—"), x.Reason,
            x.InvitedBy, names.GetValueOrDefault(x.InvitedBy, "—"), x.InvitedAt, x.RespondBy, x.Status, x.RespondedAt, x.DeclineReason)).ToList();
    }

    public async Task<IReadOnlyList<LeaveRequestDto>> LeaveDtosAsync(IReadOnlyList<LeaveRequest> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var makeupIds = list.Where(x => x.MakeupSessionId is not null).Select(x => x.MakeupSessionId!.Value).ToList();
        var makeups = makeupIds.Count == 0 ? []
            : await db.ClassSessions.AsNoTracking().Where(s => makeupIds.Contains(s.Id))
                .Select(s => new { s.Id, s.ClassId, s.Date, s.StartTime, s.EndTime }).ToListAsync(ct);
        var ctx = await ContextAsync(list.Select(x => x.SessionId), list.Select(x => x.ClassId).Concat(makeups.Select(m => m.ClassId)), ct);
        var names = await people.OfAsync(list.Select(x => x.DecidedBy), ct);
        var students = await enrollments.NamesAsync(list.Select(x => x.StudentId).ToList(), ct);
        var makeupMap = makeups.ToDictionary(m => m.Id, m => new MakeupSessionDto(m.Id.Value, m.ClassId.Value, ctx.Code(m.ClassId), m.Date,
            TimeText.Of(m.StartTime), TimeText.Of(m.EndTime)));

        return list.OrderByDescending(x => x.RequestedAt).Select(x => new LeaveRequestDto(
            x.Id.Value, x.StudentId, students.GetValueOrDefault(x.StudentId, "—"), x.ClassId.Value, ctx.Code(x.ClassId), x.BranchId.Value,
            x.SessionId.Value, ctx.Session(x.SessionId), x.ReasonType, x.Reason, x.By, x.ByName, x.Status, x.RequestedAt, x.DecidedBy,
            x.DecidedBy is { } d ? names.GetValueOrDefault(d, "—") : null, x.DecidedAt, x.MakeupSessionId?.Value,
            x.MakeupSessionId is { } m ? makeupMap.GetValueOrDefault(m) : null, x.MakeupResult, x.Note)).ToList();
    }

    private async Task<Context> ContextAsync(IEnumerable<ClassSessionId> sessionIds, IEnumerable<ClassId> classIds, CancellationToken ct)
    {
        var sids = sessionIds.Distinct().ToList();
        var cids = classIds.Distinct().ToList();
        var sessions = await db.ClassSessions.AsNoTracking().Where(s => sids.Contains(s.Id))
            .Select(s => new { s.Id, s.No, s.Date, s.StartTime, s.EndTime, s.Status }).ToListAsync(ct);
        var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => cids.Contains(c.Id)).Select(c => new { c.Id, c.Code })
            .ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        return new Context(classes, sessions.ToDictionary(s => s.Id,
            s => new RequestSessionDto(s.No, s.Date, TimeText.Of(s.StartTime), TimeText.Of(s.EndTime), s.Status)));
    }

    private sealed record Context(IReadOnlyDictionary<ClassId, string> Classes, IReadOnlyDictionary<ClassSessionId, RequestSessionDto> Sessions)
    {
        public string Code(ClassId id) => Classes.GetValueOrDefault(id, "—");
        public RequestSessionDto? Session(ClassSessionId id) => Sessions.GetValueOrDefault(id);
    }
}
