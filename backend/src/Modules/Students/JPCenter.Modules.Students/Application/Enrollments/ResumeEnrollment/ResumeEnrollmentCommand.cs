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
namespace JPCenter.Modules.Students.Application.Enrollments.ResumeEnrollment;

/// <summary>HV-08 — học lại sau bảo lưu; lớp đã kết thúc / hủy → phải chuyển lớp (E2)</summary>
public sealed record ResumeEnrollmentCommand(Guid Id) : ICommand<EnrollmentResult>;

public sealed class ResumeEnrollmentHandler(IStudentsDbContext db, EnrollmentRules rules, EnrollmentReader reader, StudentAccess access,
    IClassLookup classes, IClock clock, IAuditTrail audit) : ICommandHandler<ResumeEnrollmentCommand, EnrollmentResult>
{
    public async Task<Result<EnrollmentResult>> Handle(ResumeEnrollmentCommand cmd, CancellationToken ct)
    {
        if (await rules.ForUpdateAsync(cmd.Id, access, ct) is not var (e, s)) return EnrollmentErrors.NotFound;
        if (await classes.FindAsync(e.ClassId, ct) is not { } cls) return EnrollmentRules.ClassNotFound;
        if (cls.IsClosed) return EnrollmentRules.ClassClosed;
        if (e.Resume(clock.Today) is { IsFailure: true } r) return r.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Học lại sau bảo lưu", s.Code, cls.Code);
        return new EnrollmentResult(await reader.ReloadAsync([e.Id], ct), []);
    }
}
