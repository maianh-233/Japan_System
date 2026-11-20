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
namespace JPCenter.Modules.Students.Application.Enrollments.Common;

/// <summary>Đọc ghi danh: thấy nếu xem được hồ sơ HV (chi nhánh HV) hoặc xem được lớp (chi nhánh lớp)</summary>
public sealed class EnrollmentReader(IStudentsDbContext db, StudentAccess access, IClassLookup classes, EmployeeNames names)
{
    public async Task<IReadOnlyList<EnrollmentDto>> ListAsync(Guid? classId, Guid? studentId, CancellationToken ct)
    {
        var query = db.Enrollments.AsNoTracking();
        if (classId is { } c) query = query.Where(e => e.ClassId == c);
        if (studentId is { } s) query = query.Where(e => e.StudentId == new StudentId(s));
        var rows = await (from e in query
                          join st in db.Students.AsNoTracking().IgnoreQueryFilters() on e.StudentId equals st.Id
                          select new { e, st.BranchId }).ToListAsync(ct);
        var visible = rows.Where(r => access.CanSeeEnrollment(r.e.BranchId.Value, r.BranchId.Value)).Select(r => r.e).ToList();
        return await ToDtosAsync(visible, ct);
    }

    public async Task<IReadOnlyList<EnrollmentDto>> ReloadAsync(IReadOnlyCollection<EnrollmentId> ids, CancellationToken ct) =>
        await ToDtosAsync(await db.Enrollments.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(ct), ct);

    private async Task<IReadOnlyList<EnrollmentDto>> ToDtosAsync(IReadOnlyList<Enrollment> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var cls = await classes.FindManyAsync(list.Select(e => e.ClassId).ToList(), ct);
        var who = await names.OfAsync(list.Where(e => e.Certificate is not null).Select(e => e.Certificate!.SentBy), ct);
        return list.OrderBy(e => e.EnrolledAt).ThenBy(e => e.Id.Value).Select(e => new EnrollmentDto(
            e.Id.Value, e.StudentId.Value, e.ClassId, cls.GetValueOrDefault(e.ClassId)?.Code, e.BranchId.Value, e.Status, e.EnrolledAt, e.EndedAt,
            e.Note, e.ReservedUntil, e.TransferredToId?.Value, e.TransferredFromId?.Value,
            e.Certificate is { } c
                ? new CertificateDto(c.Code, c.IssuedAt, c.SentTo, who.GetValueOrDefault(c.SentBy) ?? "—", c.SendCount, c.Exception)
                : null)).ToList();
    }
}
