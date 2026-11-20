using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.ClassSessions.Common;

/// <summary>
/// DT-18 bước 4: điểm danh HV lớp khác đến học bù → ghi kết quả vào đơn xin nghỉ (<c>makeup_result</c>) và buổi gốc
/// (có mặt / muộn → buổi vắng gốc tính "đã học bù"). Gọi sau khi điểm danh, trước khi lưu.
/// </summary>
public sealed class MakeupSync(IAcademicDbContext db)
{
    public async Task ApplyAsync(ClassSession makeupSession, IReadOnlyCollection<Guid> studentIds, CancellationToken ct)
    {
        var guests = makeupSession.Attendance.Where(a => a.IsGuest && a.LeaveRequestId is not null && studentIds.Contains(a.StudentId)).ToList();
        if (guests.Count == 0) return;

        var leaveIds = guests.Select(g => new LeaveRequestId(g.LeaveRequestId!.Value)).ToList();
        var leaves = await db.LeaveRequests.Where(l => leaveIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id.Value, ct);
        var originalIds = guests.Select(g => g.MakeupForSessionId!.Value).Distinct().ToList();
        var originals = await db.ClassSessions.Where(s => originalIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);

        foreach (var g in guests)
        {
            if (leaves.TryGetValue(g.LeaveRequestId!.Value, out var leave)) leave.RecordMakeupResult(g.Status);
            if (g.Status is AttendanceStatus.Present or AttendanceStatus.Late
                && originals.TryGetValue(g.MakeupForSessionId!.Value, out var original))
                original.MarkMadeUp(g.StudentId, makeupSession.Id, g.Status);
        }
    }
}
