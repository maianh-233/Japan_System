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
namespace JPCenter.Modules.Students.Application.Enrollments.ReserveEnrollment;

/// <summary>HV-07 — bảo lưu đến ngày (sau hôm nay, ≤ 6 tháng) + lý do; vẫn giữ chỗ, hóa đơn giữ nguyên</summary>
public sealed record ReserveEnrollmentCommand(Guid Id, DateOnly Until, string Reason) : ICommand<EnrollmentResult>;

public sealed class ReserveEnrollmentHandler(IStudentsDbContext db, EnrollmentRules rules, EnrollmentReader reader, StudentAccess access,
    IClock clock, IAuditTrail audit) : ICommandHandler<ReserveEnrollmentCommand, EnrollmentResult>
{
    public async Task<Result<EnrollmentResult>> Handle(ReserveEnrollmentCommand cmd, CancellationToken ct)
    {
        if (await rules.ForUpdateAsync(cmd.Id, access, ct) is not var (e, s)) return EnrollmentErrors.NotFound;
        if (e.Reserve(cmd.Until, cmd.Reason ?? "", clock.Today) is { IsFailure: true } r) return r.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Bảo lưu", s.Code, e.Note);
        return new EnrollmentResult(await reader.ReloadAsync([e.Id], ct), []);
    }
}
