using FluentValidation;
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

namespace JPCenter.Modules.Students.Application.Certificates.IssueCertificates;

/// <summary>
/// HV-15 bước 2 — "Cấp &amp; gửi" nhiều HV 1 lần (tab Chờ cấp): chỉ HV đủ điều kiện (cấp ngoại lệ làm từng người), mỗi HV 1 email.
/// HV không cấp được (ngoài phạm vi, chưa đủ điều kiện, đã cấp, thiếu email…) → <c>skipped</c> kèm mã lỗi, phần còn lại vẫn cấp.
/// Mã chứng nhận nối tiếp nhau trong năm. Lỗi gửi email → <c>emailSent = false</c>, chứng nhận vẫn giữ (E4).
/// </summary>
public sealed record IssueCertificatesCommand(IReadOnlyList<CertificateTarget> Items) : ICommand<CertificatesIssueResult>;

public sealed class IssueCertificatesValidator : AbstractValidator<IssueCertificatesCommand>
{
    public IssueCertificatesValidator() =>
        RuleFor(x => x.Items).NotEmpty().WithMessage("Chọn ít nhất 1 học viên").Must(x => x.Count <= 200).WithMessage("Tối đa 200 học viên / lần");
}

public sealed class IssueCertificatesHandler(IStudentsDbContext db, StudentAccess access, CertificateDesk desk, IClassLookup classes,
    EnrollmentReader reader, IAuditTrail audit, IClock clock) : ICommandHandler<IssueCertificatesCommand, CertificatesIssueResult>
{
    public async Task<Result<CertificatesIssueResult>> Handle(IssueCertificatesCommand cmd, CancellationToken ct)
    {
        var ids = cmd.Items.Select(i => new EnrollmentId(i.EnrollmentId)).Distinct().ToList();
        var list = await db.Enrollments.Where(e => ids.Contains(e.Id)).ToListAsync(ct);
        var inScope = list.Where(e => access.InBranch(StudentAccess.Certificates, PermissionAction.Create, e.BranchId.Value)).ToDictionary(e => e.Id);
        var checks = await desk.EvaluateAsync(inScope.Values.ToList(), ct);
        var studentIds = inScope.Values.Select(e => e.StudentId).Distinct().ToList();
        var students = await db.Students.AsNoTracking().IgnoreQueryFilters().Where(s => studentIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var cls = await classes.FindManyAsync(inScope.Values.Select(e => e.ClassId).Distinct().ToList(), ct);

        var year = clock.Today.Year;
        var seq = await desk.LastSequenceAsync(year, ct);
        var issued = new List<Enrollment>();
        var skipped = new List<CertificateSkipped>();
        foreach (var item in cmd.Items.DistinctBy(i => i.EnrollmentId))
        {
            if (!inScope.TryGetValue(new EnrollmentId(item.EnrollmentId), out var e))
            {
                skipped.Add(new CertificateSkipped(item.EnrollmentId, EnrollmentErrors.NotFound.Code, EnrollmentErrors.NotFound.Message));
                continue;
            }
            var code = CertificateCode.Build(year, cls.GetValueOrDefault(e.ClassId)?.Code ?? "LOP", seq + 1);
            var r = e.IssueCertificate(code, clock.UtcNow, item.Email, access.ActorId, CertificateEligibility.IsEligible(checks[e.Id]), null);
            if (r.IsFailure)
            {
                skipped.Add(new CertificateSkipped(item.EnrollmentId, r.Error.Code, r.Error.Message));
                continue;
            }
            seq++;
            issued.Add(e);
            audit.Record(AuditCategory.Data, AuditSeverity.Info, "Cấp chứng nhận hoàn thành", $"{students[e.StudentId].Code} · {code}", branchId: e.BranchId.Value);
        }
        await db.SaveChangesAsync(ct);

        var sent = new Dictionary<EnrollmentId, bool>();
        foreach (var e in issued)
        {
            var c = cls.GetValueOrDefault(e.ClassId);
            sent[e.Id] = await desk.SendAsync(e.Certificate!.SentTo, students[e.StudentId].FullName, c?.Code, c?.Name, e.Certificate, ct);
        }
        var dtos = issued.Count == 0 ? [] : await reader.ReloadAsync(issued.Select(e => e.Id).ToList(), ct);
        return new CertificatesIssueResult(dtos.Select(d => new CertificateSendResult(d, sent[new EnrollmentId(d.Id)])).ToList(), skipped);
    }
}
