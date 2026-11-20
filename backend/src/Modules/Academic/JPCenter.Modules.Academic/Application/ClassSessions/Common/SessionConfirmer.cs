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
namespace JPCenter.Modules.Academic.Application.ClassSessions.Common;

/// <summary>
/// DT-15 — xác nhận 1 buổi (dùng chung cho xác nhận lẻ và hàng loạt). Chặn: chưa hết giờ, chưa điểm danh, đã hủy, đã xác nhận.
/// Lưu ý không chặn: GV chưa check-in / check-out, chưa có báo cáo buổi. Phát <c>SessionConfirmed</c> cho Finance (lương GV).
/// </summary>
public sealed class SessionConfirmer(AcademicAccess access, IClock clock, IIntegrationEventPublisher events)
{
    public Result<IReadOnlyList<string>> Confirm(ClassSession s, Class cls, string? note)
    {
        if (s.Confirm(access.ActorId, clock.UtcNow, note, clock.LocalNow) is { IsFailure: true } r) return r.Error;
        var warnings = new List<string>();
        var staff = s.TeacherAttendance.Where(t => t.Status != TeacherAttendanceStatus.Absent).ToList();
        if (s.TeacherAttendance.Count == 0) warnings.Add("Chưa chấm công giáo viên");
        else if (staff.Any(t => t.CheckIn is null || t.CheckOut is null)) warnings.Add("Giáo viên thiếu giờ check-in / check-out");
        if (s.Report is null) warnings.Add("Giáo viên chưa gửi báo cáo buổi");
        events.Publish(new Ev.SessionConfirmed(s.Id.Value, cls.Id.Value, s.BranchId.Value, s.Date, cls.Online,
            staff.Select(t => new Ev.ConfirmedStaff(t.TeacherId, t.Role.ToString().ToLowerInvariant())).ToList()));
        return warnings;
    }
}
