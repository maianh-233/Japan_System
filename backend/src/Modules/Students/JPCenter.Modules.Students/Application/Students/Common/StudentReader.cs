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
namespace JPCenter.Modules.Students.Application.Students.Common;

/// <summary>Đọc hồ sơ HV ra DTO theo phạm vi <c>students.profiles.view</c> (chi nhánh của HV); trạng thái học suy từ ghi danh.</summary>
public sealed class StudentReader(IStudentsDbContext db, StudentAccess access)
{
    public async Task<IReadOnlyList<StudentDto>> ListAsync(bool includeHidden, Guid? branchId, CancellationToken ct)
    {
        var allowed = access.Branches(StudentAccess.Profiles, PermissionAction.View);
        var source = db.Students.AsQueryable();
        if (!includeHidden) source = source.Where(s => !s.Hidden);
        if (branchId is { } b) source = source.Where(s => s.BranchId == new BranchId(b));
        var rows = (await Query(source).ToListAsync(ct)).Where(r => allowed is null || allowed.Contains(r.Student.BranchId.Value)).ToList();
        return await ToDtosAsync(rows, ct);
    }

    public async Task<StudentDto?> FindAsync(Guid id, CancellationToken ct)
    {
        var row = await Query(db.Students.Where(s => s.Id == new StudentId(id))).FirstOrDefaultAsync(ct);
        if (row is null || !access.InBranch(StudentAccess.Profiles, PermissionAction.View, row.Student.BranchId.Value)) return null;
        return (await ToDtosAsync([row], ct))[0];
    }

    public async Task<StudentDto> ReloadAsync(StudentId id, CancellationToken ct) =>
        (await ToDtosAsync([await Query(db.Students.Where(s => s.Id == id)).FirstAsync(ct)], ct))[0];

    public async Task<IReadOnlyList<StudentDto>> ReloadManyAsync(IReadOnlyCollection<StudentId> ids, CancellationToken ct) =>
        await ToDtosAsync(await Query(db.Students.Where(s => ids.Contains(s.Id))).ToListAsync(ct), ct);

    /// <summary>Trạng thái học tổng hợp: đang học › bảo lưu › đã học xong › đã nghỉ › chưa xếp lớp</summary>
    public static string StudyStatusOf(IReadOnlyCollection<EnrollmentStatus> statuses) =>
        statuses.Contains(EnrollmentStatus.Studying) ? "studying"
        : statuses.Contains(EnrollmentStatus.Reserved) ? "reserved"
        : statuses.Contains(EnrollmentStatus.Completed) ? "finished"
        : statuses.Count > 0 ? "dropped"
        : "none";

    private async Task<IReadOnlyList<StudentDto>> ToDtosAsync(IReadOnlyList<Row> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(r => r.Student.Id).ToList();
        var enrollments = (await db.Enrollments.AsNoTracking().Where(e => ids.Contains(e.StudentId))
                .Select(e => new { e.StudentId, e.ClassId, e.Status }).ToListAsync(ct))
            .ToLookup(e => e.StudentId);
        return rows.Select(r =>
        {
            var s = r.Student;
            var mine = enrollments[s.Id].ToList();
            var active = mine.Where(e => e.Status is EnrollmentStatus.Studying or EnrollmentStatus.Reserved).Select(e => e.ClassId).Distinct().ToList();
            return new StudentDto(s.Id.Value, s.Code, s.FullName, s.Gender, s.Dob, s.Phone, s.Email, s.Address, s.Note, s.BranchId.Value, s.JoinedAt,
                s.Hidden, s.HiddenAt, StudyStatusOf(mine.Select(e => e.Status).ToList()), active, r.Version);
        }).OrderBy(d => d.Code, StringComparer.Ordinal).ToList();
    }

    private static IQueryable<Row> Query(IQueryable<Student> source) =>
        source.AsNoTracking().Select(s => new Row(s, EF.Property<uint>(s, "xmin")));

    private sealed record Row(Student Student, uint Version);
}
