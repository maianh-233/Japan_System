using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;

namespace JPCenter.Modules.Academic.Application.ClassSessions.Common;

/// <summary>
/// Chấm công GV đi kèm sự cố giữa buổi (DT-13) — giống <c>IncidentModal.tsx</c> / <c>SessionSupport.tsx</c> ở frontend:
/// báo sự cố → GV đứng lớp "dừng dạy lúc …", người dạy thay vào lớp; học tiếp → GV cũ dạy tiếp hoặc người khác vào; kết thúc sớm → mọi người ra lúc sự cố.
/// Ghi bằng nguồn <see cref="CheckSource.Staff"/> (giáo vụ chấm hộ); dòng không hợp lệ (giờ ra ≤ giờ vào) bỏ qua, không chặn việc báo sự cố.
/// </summary>
public static class IncidentAttendance
{
    public static string ReasonText(IncidentReason r) => r switch
    {
        IncidentReason.Faint => "Ngất / choáng",
        IncidentReason.Sick => "Ốm",
        IncidentReason.Family => "Việc gia đình",
        IncidentReason.Accident => "Tai nạn",
        IncidentReason.Tech => "Sự cố kỹ thuật",
        _ => "Sự cố khác",
    };

    public static void OnReport(ClassSession s, Class c, SessionIncident inc, TimeOnly? takeoverAt, DateTimeOffset stamp)
    {
        var at = inc.At;
        var reason = ReasonText(inc.Reason);
        var leads = SessionReader.Teachers(s, c);
        foreach (var id in leads)
        {
            var row = s.TeacherAttendance.FirstOrDefault(t => t.TeacherId == id);
            s.RecordStaffAttendance(id, row?.Role ?? StaffRole.Teacher, CheckSource.Staff, row?.Status is { } st && st != TeacherAttendanceStatus.Absent ? st : TeacherAttendanceStatus.Present,
                row?.CheckIn ?? s.StartTime, at, null, $"{reason} — dừng dạy lúc {TimeText.Of(at)}", stamp);
        }
        if (inc.Action == IncidentAction.Substitute && inc.TakeoverId is { } sub)
        {
            var from = takeoverAt ?? at.AddMinutes(10);
            s.RecordStaffAttendance(sub, StaffRole.Substitute, CheckSource.Staff, TeacherAttendanceStatus.Present, from, null, null,
                $"Dạy thay từ {TimeText.Of(from)} (GV gặp sự cố)", stamp);
        }
        if (inc.Action == IncidentAction.Assistant && inc.TakeoverId is { } ta)
        {
            var row = s.TeacherAttendance.FirstOrDefault(t => t.TeacherId == ta);
            s.RecordStaffAttendance(ta, StaffRole.Assistant, CheckSource.Staff, row?.Status ?? TeacherAttendanceStatus.Present, row?.CheckIn ?? s.StartTime,
                row?.CheckOut, null, $"Đứng lớp thay GV từ {TimeText.Of(at)}", stamp);
        }
        if (inc.Action == IncidentAction.EndEarly)
            foreach (var row in s.TeacherAttendance.Where(t => !leads.Contains(t.TeacherId) && t.Status != TeacherAttendanceStatus.Absent).ToList())
                s.RecordStaffAttendance(row.TeacherId, row.Role, CheckSource.Staff, row.Status, row.CheckIn ?? s.StartTime, at, null, row.Note, stamp);
    }

    /// <summary>Học tiếp lúc <paramref name="at"/>: GV cũ (null) mở lại giờ ra; người khác → thêm dòng vào lớp từ giờ đó</summary>
    public static void OnResume(ClassSession s, Class c, TimeOnly at, Guid? other, TimeOnly pausedAt, DateTimeOffset stamp)
    {
        var leads = SessionReader.Teachers(s, c);
        if (other is not { } next)
        {
            foreach (var row in s.TeacherAttendance.Where(t => leads.Contains(t.TeacherId)).ToList())
                s.RecordStaffAttendance(row.TeacherId, row.Role, CheckSource.Staff, row.Status, row.CheckIn, null, null,
                    $"Tạm dừng {TimeText.Of(pausedAt)}–{TimeText.Of(at)} (sự cố), dạy tiếp", stamp);
            return;
        }
        if (s.TeacherAttendance.Any(t => t.TeacherId == next)) return;
        var assistants = s.AssistantIds ?? c.AssistantIds;
        s.RecordStaffAttendance(next, assistants.Contains(next) ? StaffRole.Assistant : StaffRole.Substitute, CheckSource.Staff,
            TeacherAttendanceStatus.Present, at, null, null, $"Dạy tiếp từ {TimeText.Of(at)} sau sự cố", stamp);
    }
}
