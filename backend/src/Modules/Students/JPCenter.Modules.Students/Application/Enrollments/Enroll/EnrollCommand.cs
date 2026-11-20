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
namespace JPCenter.Modules.Students.Application.Enrollments.Enroll;

/// <summary>
/// HV-06 — ghi danh HV có sẵn vào lớp (chờ khai giảng / đang học, còn chỗ, chưa có trong lớp). HV đã ẩn → tự bỏ ẩn (E5).
/// Phát <c>StudentEnrolled</c> (học phí <paramref name="Amount"/> — bỏ trống = học phí lớp; giảm trừ; hạn đóng) → Finance lập hóa đơn.
/// </summary>
public sealed record EnrollCommand(Guid StudentId, Guid ClassId, decimal? Amount, decimal? Discount, DateOnly? DueDate) : ICommand<EnrollmentResult>;

public sealed class EnrollValidator : AbstractValidator<EnrollCommand>
{
    public EnrollValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).Must(a => a is null || decimal.Truncate(a.Value) == a).WithMessage("Học phí VND không có số lẻ")
            .When(x => x.Amount is not null);
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).When(x => x.Discount is not null);
    }
}

public sealed class EnrollHandler(IStudentsDbContext db, EnrollmentRules rules, EnrollmentReader reader, StudentAccess access, IClock clock,
    IIntegrationEventPublisher events, IAuditTrail audit) : ICommandHandler<EnrollCommand, EnrollmentResult>
{
    public async Task<Result<EnrollmentResult>> Handle(EnrollCommand cmd, CancellationToken ct)
    {
        var student = await db.Students.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == new StudentId(cmd.StudentId), ct);
        if (student is null) return StudentErrors.NotFound;
        var cls = await rules.ClassForNewAsync(cmd.ClassId, 1, ct);
        if (cls.IsFailure) return cls.Error;
        if (!access.CanManageEnrollment(cls.Value.BranchId, student.BranchId.Value)) return StudentAccess.BranchNotAllowed;
        if (await rules.HasActiveAsync(student.Id, cmd.ClassId, ct)) return EnrollmentErrors.Duplicate;

        var warnings = new List<string>();
        if (student.Hidden)
        {
            student.Unhide();
            warnings.Add("Học viên đã được bỏ ẩn");
        }
        var e = Enrollment.Create(student.Id, cmd.ClassId, new BranchId(cls.Value.BranchId), clock.Today);
        db.Enrollments.Add(e);
        var amount = cmd.Amount ?? cls.Value.Fee;
        events.Publish(new Ev.StudentEnrolled(e.Id.Value, student.Id.Value, cmd.ClassId, cls.Value.BranchId, amount,
            Math.Min(amount, cmd.Discount ?? 0), cmd.DueDate, null));
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Ghi danh học viên", $"{student.Code} → {cls.Value.Code}", student.FullName);
        return new EnrollmentResult(await reader.ReloadAsync([e.Id], ct), warnings);
    }
}
