using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Students.Contracts.IntegrationEvents;
using JPCenter.Modules.Academic.Contracts.Dtos;

namespace JPCenter.Modules.Students.Application.Enrollments.Common;

/// <summary>Quy tắc ghi danh cần lớp (Academic.Contracts) + ghi danh khác: lớp nhận HV, còn chỗ, không trùng (HV-06.E1 → E3, HV-09.E2)</summary>
public sealed class EnrollmentRules(IStudentsDbContext db, IClassLookup classes)
{
    public static readonly Error ClassNotFound = Error.NotFound("academic.class.not_found", "Không tìm thấy lớp học");
    public static readonly Error ClassClosed = Error.Conflict("academic.class.closed", "Lớp đã kết thúc, đã hủy hoặc đang tạm dừng — không nhận học viên");

    public static Error ClassFull(string code, int left) =>
        Error.Conflict("academic.class.full", left <= 0 ? $"Lớp {code} đã đủ sĩ số tối đa" : $"Lớp {code} chỉ còn {left} chỗ");

    /// <summary>Lớp nhận thêm <paramref name="adding"/> HV: chờ khai giảng / đang học và còn đủ chỗ (ghi danh hiệu lực + thêm ≤ max)</summary>
    public async Task<Result<ClassSummary>> ClassForNewAsync(Guid classId, int adding, CancellationToken ct)
    {
        var cls = await classes.FindAsync(classId, ct);
        if (cls is null) return ClassNotFound;
        if (!cls.AcceptsStudents) return ClassClosed;
        var active = await ActiveCountAsync(classId, ct);
        return active + adding > cls.MaxStudents ? ClassFull(cls.Code, cls.MaxStudents - active) : cls;
    }

    public Task<int> ActiveCountAsync(Guid classId, CancellationToken ct) =>
        db.Enrollments.CountAsync(e => e.ClassId == classId && (e.Status == EnrollmentStatus.Studying || e.Status == EnrollmentStatus.Reserved), ct);

    /// <summary>HV đã có ghi danh hiệu lực ở lớp này (E3)</summary>
    public Task<bool> HasActiveAsync(StudentId studentId, Guid classId, CancellationToken ct) =>
        db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.ClassId == classId
            && (e.Status == EnrollmentStatus.Studying || e.Status == EnrollmentStatus.Reserved), ct);

    /// <summary>Ghi danh được theo dõi + HV của nó, trong quyền thao tác ghi danh</summary>
    public async Task<(Enrollment Enrollment, Student Student)?> ForUpdateAsync(Guid id, StudentAccess access, CancellationToken ct)
    {
        var e = await db.Enrollments.FirstOrDefaultAsync(x => x.Id == new EnrollmentId(id), ct);
        if (e is null) return null;
        var s = await db.Students.IgnoreQueryFilters().FirstAsync(x => x.Id == e.StudentId, ct);
        return access.CanManageEnrollment(e.BranchId.Value, s.BranchId.Value) ? (e, s) : null;
    }

    /// <summary>Sự kiện cho Finance khi ghi danh kết thúc</summary>
    public static Ev.EnrollmentEnded Ended(Enrollment e, string? reason) => new(e.Id.Value, e.StudentId.Value, e.ClassId,
        e.Status.ToString().ToLowerInvariant(), e.EndedAt ?? default, reason);
}
