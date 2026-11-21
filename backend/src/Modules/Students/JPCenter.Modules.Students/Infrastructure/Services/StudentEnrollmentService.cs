using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Students.Contracts.Dtos;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.Modules.Students.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Students.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Students.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="IStudentEnrollmentService"/> (CRM-07): tạo / chọn HV → kiểm lớp (nhận HV, còn chỗ, chưa học lớp đó) →
/// ghi danh "đang học" → phát <c>StudentEnrolled</c> (Finance lập hóa đơn) — 1 lần lưu = 1 giao dịch của Students.
/// </summary>
internal sealed class StudentEnrollmentService(StudentsDbContext db, StudentCodes codes, EnrollmentRules rules, IClock clock,
    IIntegrationEventPublisher events, IAuditTrail audit) : IStudentEnrollmentService
{
    public static readonly Error DiscountTooLarge = Error.Validation("finance.invoice.discount_too_large", "Giảm giá không được lớn hơn học phí");
    public static readonly Error InvalidAmount = Error.Validation("finance.invoice.invalid_amount", "Số tiền không hợp lệ");

    public async Task<Result<LeadEnrollmentResult>> EnrollFromLeadAsync(LeadEnrollmentRequest r, CancellationToken ct)
    {
        var today = clock.Today;
        if (r.Discount < 0 || r.Amount is < 0 || decimal.Truncate(r.Discount) != r.Discount) return InvalidAmount;

        // ---- Lớp (kiểm trước khi tạo hồ sơ — lỗi lớp thì không tạo gì) ----
        JPCenter.Modules.Academic.Contracts.Dtos.ClassSummary? cls = null;
        if (r.ClassId is { } classId)
        {
            var found = await rules.ClassForNewAsync(classId, 1, ct);
            if (found.IsFailure) return found.Error;
            cls = found.Value;
            if (r.Discount > (r.Amount ?? cls.Fee)) return DiscountTooLarge;
        }

        // ---- Học viên ----
        Student student;
        var created = false;
        if (r.ExistingStudentId is { } sid)
        {
            var existing = await db.Students.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == new StudentId(sid), ct);
            if (existing is null) return StudentErrors.NotFound;
            student = existing;
            if (cls is not null && await rules.HasActiveAsync(student.Id, cls.Id, ct)) return EnrollmentErrors.Duplicate;
            if (student.Hidden && cls is not null) student.Unhide();
        }
        else if (r.NewStudent is { } n)
        {
            Gender? gender = string.IsNullOrWhiteSpace(n.Gender) ? null : SnakeCaseEnumConverter<Gender>.Parse(n.Gender);
            var made = Student.Create(Student.CodeFor(await codes.NextNumberAsync(ct)), n.FullName, gender, n.Dob, n.Phone, n.Email, null,
                n.Note, new BranchId(n.BranchId), today, today);
            if (made.IsFailure) return made.Error;
            student = made.Value;
            db.Students.Add(student);
            created = true;
        }
        else return StudentErrors.NameRequired;

        // ---- Ghi danh + hóa đơn (Finance nghe StudentEnrolled) ----
        Enrollment? enrollment = null;
        decimal? amount = null;
        if (cls is not null)
        {
            enrollment = Enrollment.Create(student.Id, cls.Id, new BranchId(cls.BranchId), today);
            db.Enrollments.Add(enrollment);
            amount = r.Amount ?? cls.Fee;
            events.Publish(new Ev.StudentEnrolled(enrollment.Id.Value, student.Id.Value, cls.Id, cls.BranchId, amount.Value, r.Discount,
                r.DueDate, null));
        }

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Ghi danh từ lead",
            cls is null ? $"{r.LeadCode} → {student.Code}" : $"{r.LeadCode} → {student.Code} → {cls.Code}", student.FullName);
        return new LeadEnrollmentResult(student.Id.Value, student.Code, student.FullName, student.Dob, created, enrollment?.Id.Value, cls?.Id,
            cls?.Code, amount, cls is null ? 0 : r.Discount);
    }
}
