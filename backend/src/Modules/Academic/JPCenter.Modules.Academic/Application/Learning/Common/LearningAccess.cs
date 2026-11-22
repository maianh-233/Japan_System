using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Learning.Common;

/// <summary>
/// Ai thao tác được nội dung học của lớp (user story 12, quy tắc vàng 3):
/// nhân viên — phạm vi quyền trang Lớp học (<c>academic.classes</c>: view xem tab · update giao bài / tạo kỳ thi / thông báo / nhập điểm);
/// GV (cổng GV, <c>asTeacher</c>) — GV chính / trợ giảng của lớp; HV / PH — lớp HV đã ghi danh (PH: của con).
/// </summary>
public sealed class LearningAccess(IAcademicDbContext db, ClassReader classes, AcademicAccess staff, PortalOwner owner, IEnrollmentLookup enrollments)
{
    public static readonly Error NotYourClass = Error.Forbidden("academic.class.not_teaching", "Bạn không dạy lớp này");

    /// <summary>ND-02.E6</summary>
    public static readonly Error NotInClass = Error.Forbidden("academic.submission.not_in_class", "Học viên không còn học lớp này");

    /// <summary>Người thao tác (Hr.Employee của nhân viên / GV)</summary>
    public Guid ActorId => staff.ActorId;

    public string ActorName => staff.ActorName;

    /// <summary>GV đang gọi từ cổng GV (Hr.Employee)</summary>
    public Guid? TeacherId => owner.TeacherId;

    /// <summary>Đang dùng cổng PH (xem hộ con — không làm bài, không tính lượt xem)</summary>
    public bool IsParent => owner.IsParent;

    public Guid AccountId => owner.AccountId;

    /// <summary>
    /// Lớp (đang theo dõi để sửa) người gọi được thao tác: nhân viên → phạm vi <paramref name="action"/> của trang lớp (ngoài phạm vi → 404);
    /// GV → mình là GV / trợ giảng của lớp (403).
    /// </summary>
    public async Task<Result<Class>> ClassAsync(Guid classId, bool asTeacher, PermissionAction action, CancellationToken ct)
    {
        if (!asTeacher)
            return await classes.ForUpdateAsync(classId, AcademicAccess.Classes, action, ct) is { } c ? c : ClassErrors.NotFound;
        var cls = await db.Classes.FirstOrDefaultAsync(x => x.Id == new ClassId(classId), ct);
        if (cls is null) return ClassErrors.NotFound;
        return Teaches(cls) ? cls : NotYourClass;
    }

    /// <summary>GV đang gọi là GV chính / trợ giảng của lớp</summary>
    public bool Teaches(Class c) => owner.TeacherId is { } me && (c.TeacherIds.Contains(me) || c.AssistantIds.Contains(me));

    /// <summary>HV tài khoản cổng đang thao tác (PH chọn 1 con) + mọi ghi danh của HV đó</summary>
    public async Task<Result<Learner>> LearnerAsync(Guid? studentId, CancellationToken ct)
    {
        var s = await owner.StudentAsync(studentId, ct);
        if (s.IsFailure) return s.Error;
        return new Learner(s.Value, await enrollments.OfStudentAsync(s.Value, ct));
    }

    /// <summary>Mọi lớp đã ghi danh của các HV thuộc tài khoản cổng (PH: mọi con)</summary>
    public async Task<IReadOnlyList<Guid>> AccountClassIdsAsync(CancellationToken ct)
    {
        var ids = new HashSet<Guid>();
        foreach (var s in await owner.StudentsAsync(ct))
            ids.UnionWith((await enrollments.OfStudentAsync(s, ct)).Select(e => e.ClassId));
        return ids.ToList();
    }

    /// <summary>HV đang học lớp (đọc lại danh sách lớp) — bỏ HV bảo lưu</summary>
    public async Task<IReadOnlyList<Guid>> StudyingAsync(ClassId classId, CancellationToken ct) =>
        (await enrollments.RosterAsync(classId.Value, ct)).Where(r => r.Status == "studying").Select(r => r.StudentId).ToList();
}
