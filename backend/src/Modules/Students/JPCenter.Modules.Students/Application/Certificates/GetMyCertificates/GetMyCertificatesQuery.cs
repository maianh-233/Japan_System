using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Certificates.Common;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Certificates.GetMyCertificates;

/// <summary>
/// HV-15.E5 — cổng HV / PH › Chứng nhận khóa học: các lớp đang học / bảo lưu / đã hoàn thành của HV (PH chọn con) kèm checklist điều kiện
/// và chứng nhận đã cấp (mã, ngày cấp, email đã gửi). Không trả người gửi / lý do cấp ngoại lệ (thông tin nội bộ).
/// </summary>
public sealed record GetMyCertificatesQuery(Guid? StudentId) : IQuery<IReadOnlyList<MyCertificateDto>>;

public sealed class GetMyCertificatesHandler(IStudentsDbContext db, LearnerPortal portal, CertificateDesk desk)
    : IQueryHandler<GetMyCertificatesQuery, IReadOnlyList<MyCertificateDto>>
{
    private static readonly EnrollmentStatus[] Shown = [EnrollmentStatus.Studying, EnrollmentStatus.Reserved, EnrollmentStatus.Completed];

    public async Task<Result<IReadOnlyList<MyCertificateDto>>> Handle(GetMyCertificatesQuery q, CancellationToken ct)
    {
        var student = await portal.StudentAsync(q.StudentId, ct);
        if (student.IsFailure) return student.Error;
        var id = new StudentId(student.Value);
        var list = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == id && Shown.Contains(e.Status)).ToListAsync(ct);
        var checks = await desk.EvaluateAsync(list, ct);
        return list.OrderByDescending(e => e.EnrolledAt).Select(e =>
        {
            var c = e.Certificate;
            return new MyCertificateDto(e.Id.Value, e.StudentId.Value, e.ClassId, e.Status, e.EndedAt, checks[e.Id],
                CertificateEligibility.IsEligible(checks[e.Id]), c is null ? null : new MyCertificateInfo(c.Code, c.IssuedAt, c.SentTo));
        }).ToList();
    }
}
