using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Certificates.Common;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Certificates.IssueCertificate;

/// <summary>
/// HV-15 bước 2 — "Cấp &amp; gửi": ghi danh phải hoàn thành (E2); chưa đủ điều kiện → cần lý do ngoại lệ (E1, nhật ký <c>warning</c>);
/// cấp 1 lần (E3); sinh mã <c>CN-{năm}-{lớp}-{số:4}</c> rồi gửi email (lỗi gửi vẫn giữ chứng nhận — E4).
/// ⏳ PDF theo mẫu chưa sinh (chưa chọn thư viện PDF) — email gửi mã + link tra cứu.
/// </summary>
public sealed record IssueCertificateCommand(Guid EnrollmentId, string? Email, string? Exception) : ICommand<CertificateSendResult>;

public sealed class IssueCertificateHandler(IStudentsDbContext db, StudentAccess access, CertificateDesk desk, IClassLookup classes,
    EnrollmentReader reader, IAuditTrail audit, IClock clock) : ICommandHandler<IssueCertificateCommand, CertificateSendResult>
{
    public async Task<Result<CertificateSendResult>> Handle(IssueCertificateCommand cmd, CancellationToken ct)
    {
        var e = await db.Enrollments.FirstOrDefaultAsync(x => x.Id == new EnrollmentId(cmd.EnrollmentId), ct);
        if (e is null || !access.InBranch(StudentAccess.Certificates, PermissionAction.Create, e.BranchId.Value)) return EnrollmentErrors.NotFound;
        var student = await db.Students.AsNoTracking().IgnoreQueryFilters().FirstAsync(s => s.Id == e.StudentId, ct);
        var cls = await classes.FindAsync(e.ClassId, ct);
        var eligible = CertificateEligibility.IsEligible((await desk.EvaluateAsync([e], ct))[e.Id]);
        var code = await desk.NextCodeAsync(clock.Today.Year, cls?.Code ?? "LOP", ct);
        var issued = e.IssueCertificate(code, clock.UtcNow, cmd.Email, access.ActorId, eligible, cmd.Exception);
        if (issued.IsFailure) return issued.Error;
        if (e.Certificate!.Exception is { } reason)
            audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Cấp chứng nhận ngoại lệ", $"{student.Code} · {code}", reason, branchId: e.BranchId.Value);
        else audit.Record(AuditCategory.Data, AuditSeverity.Info, "Cấp chứng nhận hoàn thành", $"{student.Code} · {code}", branchId: e.BranchId.Value);
        await db.SaveChangesAsync(ct);
        var sent = await desk.SendAsync(e.Certificate.SentTo, student.FullName, cls?.Code, cls?.Name, e.Certificate, ct);
        return new CertificateSendResult((await reader.ReloadAsync([e.Id], ct))[0], sent);
    }
}
