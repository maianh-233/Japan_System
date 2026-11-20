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
namespace JPCenter.Modules.Academic.Application.ClassSessions.CheckSession;

/// <summary>
/// Dò trùng khi xếp 1 buổi (học bù / học thêm / dời lịch) — chỉ cảnh báo. GV / phòng bỏ trống = theo lớp.
/// <paramref name="ExceptId"/> = buổi đang sửa.
/// </summary>
public sealed record CheckSessionQuery(Guid ClassId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, List<Guid>? TeacherIds,
    Guid? RoomId, Guid? ExceptId) : IQuery<IReadOnlyList<string>>;

public sealed class CheckSessionHandler(ClassReader classes, SessionConflicts conflicts) : IQueryHandler<CheckSessionQuery, IReadOnlyList<string>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(CheckSessionQuery q, CancellationToken ct)
    {
        if (q.EndTime <= q.StartTime) return SessionErrors.InvalidTime;
        var cls = await classes.ForUpdateAsync(q.ClassId, AcademicAccess.Sessions, PermissionAction.View, ct);
        if (cls is null) return ClassErrors.NotFound;
        var people = (q.TeacherIds is { Count: > 0 } t ? t : cls.TeacherIds).Concat(cls.AssistantIds).Distinct().ToList();
        var room = cls.Online ? null : q.RoomId ?? cls.RoomId?.Value;
        return Result.Success(await conflicts.WarningsAsync(new SessionProbe(q.Date, q.StartTime, q.EndTime, people, room,
            q.ExceptId is { } x ? new ClassSessionId(x) : null), ct));
    }
}
