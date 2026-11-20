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
namespace JPCenter.Modules.Students.Application.Enrollments.TransferEnrollments;

/// <summary>1 HV chuyển: học phí lớp mới + khấu trừ (đã đóng − đã dùng) do frontend gợi ý, người dùng sửa được</summary>
public sealed record TransferItem(Guid EnrollmentId, decimal Amount, decimal Credit);

/// <summary>
/// HV-09 — chuyển 1 hoặc nhiều HV sang 1 lớp đích trong 1 giao dịch. Ghi danh cũ → transferred (nối sang ghi danh mới ở lớp đích).
/// Phát <c>EnrollmentEnded</c> (cũ) + <c>StudentEnrolled</c> (mới, giảm trừ = min(học phí, khấu trừ)) → Finance hủy phần còn lại / lập hóa đơn mới.
/// Khấu trừ &gt; học phí → cảnh báo hoàn tay phần dư (E4).
/// </summary>
public sealed record TransferEnrollmentsCommand(Guid TargetClassId, List<TransferItem> Items, DateOnly? DueDate, string Reason)
    : ICommand<EnrollmentResult>;

public sealed class TransferEnrollmentsValidator : AbstractValidator<TransferEnrollmentsCommand>
{
    public TransferEnrollmentsValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Chọn học viên cần chuyển").Must(i => i.Count <= 100);
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
            i.RuleFor(x => x.Credit).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Cần ghi lý do chuyển lớp").MaximumLength(500);
    }
}

public sealed class TransferEnrollmentsHandler(IStudentsDbContext db, EnrollmentRules rules, EnrollmentReader reader, StudentAccess access,
    IClassLookup classes, IClock clock, IIntegrationEventPublisher events, IAuditTrail audit)
    : ICommandHandler<TransferEnrollmentsCommand, EnrollmentResult>
{
    public async Task<Result<EnrollmentResult>> Handle(TransferEnrollmentsCommand cmd, CancellationToken ct)
    {
        var items = cmd.Items.GroupBy(i => i.EnrollmentId).Select(g => g.First()).ToList();
        var target = await rules.ClassForNewAsync(cmd.TargetClassId, items.Count, ct);
        if (target.IsFailure) return target.Error;
        var to = target.Value;

        var loaded = new List<(Enrollment Old, Student Student, TransferItem Item)>();
        foreach (var item in items)
        {
            if (await rules.ForUpdateAsync(item.EnrollmentId, access, ct) is not var (e, s)) return EnrollmentErrors.NotFound;
            if (!e.IsActive) return EnrollmentErrors.AlreadyEnded;
            if (e.ClassId == to.Id) return EnrollmentErrors.SameClass;
            if (!access.CanManageEnrollment(to.BranchId, s.BranchId.Value)) return StudentAccess.BranchNotAllowed;
            if (await rules.HasActiveAsync(s.Id, to.Id, ct)) return EnrollmentErrors.Duplicate;
            loaded.Add((e, s, item));
        }

        var fromCodes = await classes.FindManyAsync(loaded.Select(x => x.Old.ClassId).ToList(), ct);
        var today = clock.Today;
        var touched = new List<EnrollmentId>();
        var warnings = new List<string>();
        foreach (var (old, student, item) in loaded)
        {
            var fromCode = fromCodes.GetValueOrDefault(old.ClassId)?.Code ?? "?";
            var next = Enrollment.Create(student.Id, to.Id, new BranchId(to.BranchId), today, old.Id, note: $"Chuyển từ lớp {fromCode} — {cmd.Reason.Trim()}");
            if (old.TransferTo(next.Id, to.Code, cmd.Reason, today) is { IsFailure: true } r) return r.Error;
            db.Enrollments.Add(next);
            events.Publish(EnrollmentRules.Ended(old, cmd.Reason.Trim()));
            events.Publish(new Ev.StudentEnrolled(next.Id.Value, student.Id.Value, to.Id, to.BranchId, item.Amount, Math.Min(item.Amount, item.Credit),
                cmd.DueDate, old.Id.Value));
            if (item.Credit > item.Amount)
                warnings.Add($"{student.Code}: khấu trừ dư {item.Credit - item.Amount:N0}đ — kế toán hoàn tay");
            touched.Add(old.Id);
            touched.Add(next.Id);
        }
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Chuyển lớp", $"{loaded.Count} học viên → {to.Code}", cmd.Reason);
        return new EnrollmentResult(await reader.ReloadAsync(touched, ct), warnings);
    }
}
