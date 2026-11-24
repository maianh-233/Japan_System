using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Certificates.Common;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Certificates.GetCertificates;

/// <summary>
/// HV-15 bước 1 — Học viên › Chứng nhận hoàn thành: HV đã hoàn thành lớp (lớp kết thúc — HV-11) kèm checklist điều kiện.
/// Phạm vi theo chi nhánh của lớp. <paramref name="Eligible"/>: true = chỉ HV đủ điều kiện chưa cấp, false = chưa đủ, null = tất cả.
/// </summary>
public sealed record GetCertificatesQuery(Guid? ClassId, bool? Eligible) : IQuery<IReadOnlyList<CertificateRowDto>>;

public sealed class GetCertificatesHandler(IStudentsDbContext db, StudentAccess access, CertificateDesk desk, IClassLookup classes, EmployeeNames names)
    : IQueryHandler<GetCertificatesQuery, IReadOnlyList<CertificateRowDto>>
{
    public async Task<Result<IReadOnlyList<CertificateRowDto>>> Handle(GetCertificatesQuery q, CancellationToken ct)
    {
        var query = db.Enrollments.AsNoTracking().Where(e => e.Status == EnrollmentStatus.Completed);
        if (q.ClassId is { } cid) query = query.Where(e => e.ClassId == cid);
        if (access.Branches(StudentAccess.Certificates, PermissionAction.View) is { } allowed)
        {
            var branches = allowed.Select(b => new BranchId(b)).ToList();
            query = query.Where(e => branches.Contains(e.BranchId));
        }
        var rows = await (from e in query
                          join s in db.Students.AsNoTracking().IgnoreQueryFilters() on e.StudentId equals s.Id
                          select new { E = e, s.Code, s.FullName, s.Email }).ToListAsync(ct);
        var checks = await desk.EvaluateAsync(rows.Select(r => r.E).ToList(), ct);
        var cls = await classes.FindManyAsync(rows.Select(r => r.E.ClassId).Distinct().ToList(), ct);
        var who = await names.OfAsync(rows.Where(r => r.E.Certificate is not null).Select(r => r.E.Certificate!.SentBy), ct);
        var result = rows.Select(r =>
        {
            var list = checks[r.E.Id];
            var c = r.E.Certificate;
            return new CertificateRowDto(r.E.Id.Value, r.E.StudentId.Value, r.Code, r.FullName, r.Email, r.E.ClassId, cls.GetValueOrDefault(r.E.ClassId)?.Code,
                cls.GetValueOrDefault(r.E.ClassId)?.Name, r.E.Status, r.E.EndedAt, list, CertificateEligibility.IsEligible(list),
                c is null ? null : new CertificateDto(c.Code, c.IssuedAt, c.SentTo, who.GetValueOrDefault(c.SentBy) ?? "—", c.SendCount, c.Exception));
        });
        if (q.Eligible is { } want) result = result.Where(r => want ? r.Eligible && r.Certificate is null : !r.Eligible);
        return result.OrderBy(r => r.ClassCode).ThenBy(r => r.StudentName).ToList();
    }
}
