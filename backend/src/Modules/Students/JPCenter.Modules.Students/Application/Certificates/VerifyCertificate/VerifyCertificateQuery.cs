using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Certificates.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Certificates.VerifyCertificate;

/// <summary>HV-15 bước 4 — tra cứu công khai theo mã (QR trên chứng nhận): họ tên, lớp, cấp độ, ngày cấp. Không lộ email / lý do ngoại lệ</summary>
public sealed record VerifyCertificateQuery(string Code) : IQuery<CertificateVerificationDto>;

public sealed class VerifyCertificateHandler(IStudentsDbContext db, IClassLookup classes) : IQueryHandler<VerifyCertificateQuery, CertificateVerificationDto>
{
    public async Task<Result<CertificateVerificationDto>> Handle(VerifyCertificateQuery q, CancellationToken ct)
    {
        var code = q.Code.Trim().ToUpperInvariant();
        if (code.Length is 0 or > 50) return CertificateErrors.InvalidCode;
        var row = await (from e in db.Enrollments.AsNoTracking().IgnoreQueryFilters()
                         join s in db.Students.AsNoTracking().IgnoreQueryFilters() on e.StudentId equals s.Id
                         where e.Certificate != null && e.Certificate.Code == code
                         select new { e.ClassId, s.FullName, e.Certificate!.IssuedAt }).FirstOrDefaultAsync(ct);
        if (row is null) return CertificateErrors.InvalidCode;
        var cls = await classes.FindAsync(row.ClassId, ct);
        return new CertificateVerificationDto(code, row.FullName, cls?.Code, cls?.Name, cls?.Level, row.IssuedAt);
    }
}
