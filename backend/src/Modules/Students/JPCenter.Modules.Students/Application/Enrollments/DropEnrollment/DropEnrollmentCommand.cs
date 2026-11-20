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
namespace JPCenter.Modules.Students.Application.Enrollments.DropEnrollment;

/// <summary>HV-10 — cho nghỉ (lý do bắt buộc) → Finance hủy phần học phí còn lại. Lần nghỉ cuối của HV → gợi ý ẩn (E3).</summary>
public sealed record DropEnrollmentCommand(Guid Id, string Reason) : ICommand<EnrollmentResult>;

public sealed class DropEnrollmentHandler(IStudentsDbContext db, EnrollmentRules rules, EnrollmentReader reader, StudentAccess access,
    IClock clock, IIntegrationEventPublisher events, IAuditTrail audit) : ICommandHandler<DropEnrollmentCommand, EnrollmentResult>
{
    public async Task<Result<EnrollmentResult>> Handle(DropEnrollmentCommand cmd, CancellationToken ct)
    {
        if (await rules.ForUpdateAsync(cmd.Id, access, ct) is not var (e, s)) return EnrollmentErrors.NotFound;
        if (e.Drop(clock.Today, cmd.Reason ?? "") is { IsFailure: true } r) return r.Error;
        events.Publish(EnrollmentRules.Ended(e, e.Note));
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Cho nghỉ học", s.Code, e.Note);

        var stillActive = await db.Enrollments.AnyAsync(x => x.StudentId == s.Id
            && (x.Status == EnrollmentStatus.Studying || x.Status == EnrollmentStatus.Reserved), ct);
        return new EnrollmentResult(await reader.ReloadAsync([e.Id], ct),
            stillActive ? [] : ["Học viên không còn học lớp nào — có thể ẩn khỏi danh sách"]);
    }
}
