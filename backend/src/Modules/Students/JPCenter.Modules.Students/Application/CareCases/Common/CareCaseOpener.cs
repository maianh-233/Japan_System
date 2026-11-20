using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.CareCases.Common;

/// <summary>Chỉ ra 1 trường hợp chăm sóc: theo Id, hoặc theo lớp × HV (chưa có thì mở bằng tay nếu HV có ghi danh ở lớp)</summary>
public sealed record CareCaseRef(Guid? Id, Guid? ClassId, Guid? StudentId);

/// <summary>
/// HV-14 — tìm trường hợp chăm sóc để sửa / ghi liên hệ, trong phạm vi chi nhánh <c>students.at-risk.update</c>.
/// Theo lớp × HV mà chưa có → mở mới (<see cref="CareCase.OpenManually"/>) — giao diện cảnh báo còn tính thêm dấu hiệu bài tập / điểm / học phí.
/// </summary>
public sealed class CareCaseOpener(IStudentsDbContext db, StudentAccess access, IClock clock)
{
    public static readonly Error NotFound = Error.NotFound("students.care_case.not_found", "Không tìm thấy trường hợp chăm sóc");

    public async Task<Result<CareCase>> OpenAsync(CareCaseRef r, CancellationToken ct)
    {
        CareCase? c;
        if (r.Id is { } id) c = await db.CareCases.FirstOrDefaultAsync(x => x.Id == new CareCaseId(id), ct);
        else if (r is { ClassId: { } classId, StudentId: { } studentId })
        {
            var sid = new StudentId(studentId);
            c = await db.CareCases.FirstOrDefaultAsync(x => x.ClassId == classId && x.StudentId == sid, ct);
            if (c is null)
            {
                var enrollment = await db.Enrollments.AsNoTracking().Where(e => e.ClassId == classId && e.StudentId == sid)
                    .OrderBy(e => e.Status == EnrollmentStatus.Studying ? 0 : 1).FirstOrDefaultAsync(ct);
                if (enrollment is null) return NotFound;
                if (!access.InBranch(StudentAccess.AtRisk, PermissionAction.Update, enrollment.BranchId.Value)) return NotFound;
                c = CareCase.OpenManually(sid, classId, enrollment.BranchId, clock.UtcNow);
                db.CareCases.Add(c);
            }
        }
        else return NotFound;

        return c is not null && access.InBranch(StudentAccess.AtRisk, PermissionAction.Update, c.BranchId.Value) ? c : NotFound;
    }
}
