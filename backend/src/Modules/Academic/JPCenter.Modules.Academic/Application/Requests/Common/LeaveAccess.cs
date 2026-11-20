using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.Common;

/// <summary>
/// Cổng HV / PH (DT-18): buổi của lớp HV đang học (ghi danh đang học / bảo lưu) và đơn xin nghỉ của HV thuộc tài khoản (PQ-11, E5).
/// </summary>
public sealed class LeaveAccess(IAcademicDbContext db, PortalOwner owner, IEnrollmentLookup enrollments)
{
    /// <summary>Buổi + lớp của HV (đã kiểm HV thuộc tài khoản và đang học lớp đó)</summary>
    public async Task<Result<(Guid StudentId, ClassSession Session, Class Class)>> SessionAsync(Guid? studentId, Guid sessionId, CancellationToken ct)
    {
        var student = await owner.StudentAsync(studentId, ct);
        if (student.IsFailure) return student.Error;
        var session = await db.ClassSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new ClassSessionId(sessionId), ct);
        if (session is null) return SessionErrors.NotFound;
        var inClass = (await enrollments.OfStudentAsync(student.Value, ct))
            .Any(e => e.ClassId == session.ClassId.Value && e.Status is "studying" or "reserved");
        if (!inClass) return SessionErrors.NotInRoster;
        var cls = await db.Classes.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == session.ClassId, ct);
        return (student.Value, session, cls);
    }

    /// <summary>Đơn (đang theo dõi để sửa) của 1 HV thuộc tài khoản</summary>
    public async Task<LeaveRequest?> OwnRequestAsync(Guid id, CancellationToken ct)
    {
        var mine = await owner.StudentsAsync(ct);
        return await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == new LeaveRequestId(id) && mine.Contains(l.StudentId), ct);
    }
}
