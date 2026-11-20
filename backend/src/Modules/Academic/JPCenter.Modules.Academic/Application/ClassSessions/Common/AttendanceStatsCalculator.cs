using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.ClassSessions;

namespace JPCenter.Modules.Academic.Application.ClassSessions.Common;

/// <summary>
/// Chuyên cần 1 HV trong 1 lớp từ các buổi đã điểm danh (khớp <c>studentInsights.ts</c> ở frontend):
/// vắng không phép chưa học bù = vắng · đã học bù / có mặt / muộn = đi học · có phép không tính vắng.
/// </summary>
public static class AttendanceStatsCalculator
{
    /// <param name="sessions">Buổi của lớp (mọi trạng thái — tự bỏ buổi hủy / chưa điểm danh)</param>
    public static AttendanceStat Compute(Guid classId, Guid studentId, IEnumerable<ClassSession> sessions)
    {
        var records = sessions
            .Where(s => s.Status != SessionStatus.Cancelled)
            .Select(s => (s.Date, s.StartTime, Record: s.Attendance.FirstOrDefault(a => a.StudentId == studentId && !a.IsGuest)))
            .Where(x => x.Record is not null)
            .OrderBy(x => x.Date).ThenBy(x => x.StartTime)
            .Select(x => x.Record!)
            .ToList();

        var streak = 0;
        for (var i = records.Count - 1; i >= 0 && Missed(records[i]); i--) streak++;
        int? rate = records.Count == 0 ? null : (int)Math.Round(records.Count(Attended) * 100m / records.Count);
        return new AttendanceStat(classId, studentId, records.Count, records.Count(Missed), streak, rate);
    }

    private static bool Missed(AttendanceRecord r) => r.Status == AttendanceStatus.Absent && r.MadeUpSessionId is null;

    private static bool Attended(AttendanceRecord r) => r.Status is AttendanceStatus.Present or AttendanceStatus.Late || r.MadeUpSessionId is not null;
}
