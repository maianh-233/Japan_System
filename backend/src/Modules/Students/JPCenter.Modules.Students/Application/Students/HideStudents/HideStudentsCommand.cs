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
namespace JPCenter.Modules.Students.Application.Students.HideStudents;

public sealed record HideStudentsResult(IReadOnlyList<StudentDto> Hidden, IReadOnlyList<SkippedStudentDto> Skipped);

/// <summary>
/// HV-05 — ẩn 1 hoặc nhiều HV (1 request — E4). HV còn ghi danh đang học / bảo lưu → bỏ qua kèm lý do (E1).
/// Còn nợ / tài khoản cổng còn hoạt động → frontend cảnh báo trước, không chặn (E2, E3).
/// </summary>
public sealed record HideStudentsCommand(List<Guid> Ids) : ICommand<HideStudentsResult>;

public sealed class HideStudentsValidator : AbstractValidator<HideStudentsCommand>
{
    public HideStudentsValidator() => RuleFor(x => x.Ids).NotEmpty().WithMessage("Chọn học viên").Must(i => i.Count <= 500);
}

public sealed class HideStudentsHandler(IStudentsDbContext db, StudentReader reader, StudentAccess access, IClassLookup classes, IClock clock,
    IAuditTrail audit) : ICommandHandler<HideStudentsCommand, HideStudentsResult>
{
    public async Task<Result<HideStudentsResult>> Handle(HideStudentsCommand cmd, CancellationToken ct)
    {
        var ids = cmd.Ids.Distinct().Select(i => new StudentId(i)).ToList();
        var students = await db.Students.Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        var active = await db.Enrollments.AsNoTracking()
            .Where(e => ids.Contains(e.StudentId) && (e.Status == EnrollmentStatus.Studying || e.Status == EnrollmentStatus.Reserved))
            .Select(e => new { e.StudentId, e.ClassId, e.Status }).ToListAsync(ct);
        var codes = await classes.FindManyAsync(active.Select(a => a.ClassId).ToList(), ct);

        var hidden = new List<StudentId>();
        var skipped = new List<SkippedStudentDto>();
        foreach (var id in cmd.Ids.Distinct())
        {
            var s = students.FirstOrDefault(x => x.Id.Value == id);
            if (s is null || !access.InBranch(StudentAccess.Profiles, PermissionAction.Update, s.BranchId.Value))
            {
                skipped.Add(new SkippedStudentDto(id, "", StudentErrors.NotFound.Message));
                continue;
            }
            var where = active.Where(a => a.StudentId == s.Id)
                .Select(a => (codes.GetValueOrDefault(a.ClassId)?.Code ?? "?") + (a.Status == EnrollmentStatus.Reserved ? " (bảo lưu)" : ""))
                .ToList();
            if (s.Hide(where, clock.UtcNow) is { IsFailure: true } r) skipped.Add(new SkippedStudentDto(id, s.Code, r.Error.Message));
            else hidden.Add(s.Id);
        }
        await db.SaveChangesAsync(ct);
        if (hidden.Count > 0) audit.Record(AuditCategory.Data, AuditSeverity.Info, "Ẩn học viên", $"{hidden.Count} học viên");
        return new HideStudentsResult(await reader.ReloadManyAsync(hidden, ct), skipped);
    }
}
