using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="IScheduleLookup"/> — phòng và buổi học chiếm phòng / GV (Crm kiểm trùng lịch hẹn).
/// Buổi không đổi riêng phòng / GV dùng phòng / GV / trợ giảng của lớp.
/// </summary>
internal sealed class ScheduleLookup(AcademicDbContext db) : IScheduleLookup
{
    public async Task<RoomSummary?> RoomAsync(Guid roomId, CancellationToken ct) => (await RoomsAsync([roomId], ct)).GetValueOrDefault(roomId);

    public async Task<IReadOnlyDictionary<Guid, RoomSummary>> RoomsAsync(IReadOnlyCollection<Guid> roomIds, CancellationToken ct)
    {
        var ids = roomIds.Distinct().Select(x => new RoomId(x)).ToList();
        if (ids.Count == 0) return new Dictionary<Guid, RoomSummary>();
        var rooms = await db.Rooms.AsNoTracking().Where(r => ids.Contains(r.Id)).ToListAsync(ct);
        return rooms.ToDictionary(r => r.Id.Value,
            r => new RoomSummary(r.Id.Value, r.Name, r.BranchId.Value, SnakeCaseEnumConverter<RoomStatus>.ToText(r.Status)));
    }

    public async Task<IReadOnlyList<BusySession>> BusyAsync(DateOnly fromDate, DateOnly toDate, Guid? roomId, Guid? teacherId, CancellationToken ct)
    {
        if (roomId is null && teacherId is null) return [];
        var rows = await (
                from s in db.ClassSessions.AsNoTracking()
                join c in db.Classes.AsNoTracking() on s.ClassId equals c.Id
                where s.Date >= fromDate && s.Date <= toDate && s.Status != SessionStatus.Cancelled
                select new { s.Id, c.Code, s.Date, s.StartTime, s.EndTime, SessionRoom = s.RoomId, ClassRoom = c.RoomId, s.TeacherIds,
                    s.AssistantIds, ClassTeachers = c.TeacherIds, ClassAssistants = c.AssistantIds })
            .ToListAsync(ct);

        var result = new List<BusySession>();
        foreach (var r in rows)
        {
            var room = (r.SessionRoom ?? r.ClassRoom)?.Value;
            var teachers = (r.TeacherIds.Count > 0 ? r.TeacherIds : r.ClassTeachers).Concat(r.AssistantIds ?? r.ClassAssistants).Distinct().ToList();
            if ((roomId is { } rid && room == rid) || (teacherId is { } tid && teachers.Contains(tid)))
                result.Add(new BusySession(r.Id.Value, r.Code, r.Date, r.StartTime, r.EndTime, room, teachers));
        }
        return result;
    }
}
