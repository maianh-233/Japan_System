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

namespace JPCenter.Modules.Students.Application.Certificates.ResendCertificate;

/// <summary>HV-15 bước 3 — gửi lại (email có thể khác), tăng số lần gửi. Chưa cấp → <c>students.certificate.not_found</c> (E6)</summary>
public sealed record ResendCertificateCommand(Guid EnrollmentId, string? Email) : ICommand<CertificateSendResult>;

public sealed class ResendCertificateHandler(IStudentsDbContext db, StudentAccess access, CertificateDesk desk, IClassLookup classes, EnrollmentReader reader)
    : ICommandHandler<ResendCertificateCommand, CertificateSendResult>
{
    public async Task<Result<CertificateSendResult>> Handle(ResendCertificateCommand cmd, CancellationToken ct)
    {
        var e = await db.Enrollments.FirstOrDefaultAsync(x => x.Id == new EnrollmentId(cmd.EnrollmentId), ct);
        if (e is null || !access.InBranch(StudentAccess.Certificates, PermissionAction.Update, e.BranchId.Value)) return EnrollmentErrors.NotFound;
        var r = e.ResendCertificate(cmd.Email, access.ActorId);
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        var student = await db.Students.AsNoTracking().IgnoreQueryFilters().FirstAsync(s => s.Id == e.StudentId, ct);
        var cls = await classes.FindAsync(e.ClassId, ct);
        var sent = await desk.SendAsync(e.Certificate!.SentTo, student.FullName, cls?.Code, cls?.Name, e.Certificate, ct);
        return new CertificateSendResult((await reader.ReloadAsync([e.Id], ct))[0], sent);
    }
}
