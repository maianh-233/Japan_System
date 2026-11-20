using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.Classes.CreateClass;

/// <summary>
/// DT-02 — mở lớp (<c>enrolling</c>). Chặn: mã trùng, khóa không bán ở chi nhánh, GV / phòng không hợp lệ, trùng lịch GV / phòng.
/// <paramref name="GenerateSessions"/> (mặc định bật) → sinh lịch buổi ngay (DT-08).
/// </summary>
public sealed record CreateClassCommand(ClassInput Input, bool GenerateSessions) : ICommand<ClassResult>;

public sealed class CreateClassValidator : AbstractValidator<CreateClassCommand>
{
    public CreateClassValidator() => RuleFor(x => x.Input).SetValidator(new ClassInputValidator());
}

public sealed class CreateClassHandler(IAcademicDbContext db, ClassRules rules, ClassReader reader, SessionScheduler scheduler,
    AcademicAccess access, IIntegrationEventPublisher events, IAuditTrail audit) : ICommandHandler<CreateClassCommand, ClassResult>
{
    public async Task<Result<ClassResult>> Handle(CreateClassCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        if (!access.InBranch(AcademicAccess.Classes, PermissionAction.Create, i.BranchId)) return AcademicAccess.BranchNotAllowed;
        if (await rules.EnsureBranchAsync(i.BranchId, null, ct) is { IsFailure: true } branch) return branch.Error;
        var course = await rules.CourseAsync(i.CourseId, i.BranchId, null, ct);
        if (course.IsFailure) return course.Error;
        if (await rules.EnsureCurriculumAsync(i.CurriculumId, course.Value, null, ct) is { IsFailure: true } cur) return cur.Error;
        var code = Class.NormalizeCode(i.Code, course.Value.TeacherType);
        if (code.IsFailure) return code.Error;
        if (await rules.EnsureCodeUniqueAsync(code.Value, null, ct) is { IsFailure: true } dup) return dup.Error;
        if (await rules.EnsureStaffAsync((i.TeacherIds ?? []).Concat(i.AssistantIds ?? []), null, ct) is { IsFailure: true } staff) return staff.Error;
        var capacity = await rules.RoomCapacityAsync(i.RoomId, i.BranchId, i.Online, null, ct);
        if (capacity.IsFailure) return capacity.Error;
        if (i.PreviousClassId is { } prev && !await db.Classes.AnyAsync(c => c.Id == new ClassId(prev), ct)) return ClassErrors.NotFound;

        var count = Class.SessionsFor(i.TotalHours, i.StartTime, i.EndTime);
        var end = i.PlannedEndDate ?? await scheduler.EndDateAsync(i.PlannedStartDate, i.Weekdays ?? [], count, ct) ?? i.PlannedStartDate;
        var created = Class.Create(course.Value.Id, i.CurriculumId is { } cid ? new Domain.Curricula.CurriculumId(cid) : null,
            course.Value.TeacherType, i.ToSetup(end), capacity.Value);
        if (created.IsFailure) return created.Error;
        var cls = created.Value;

        var conflicts = await rules.ConflictsAsync(new ClassScheduleProbe(cls.TeacherIds.Concat(cls.AssistantIds).ToList(), cls.RoomId?.Value,
            cls.PlannedStartDate, cls.PlannedEndDate, cls.Weekdays, cls.StartTime, cls.EndTime), null, ct);
        if (conflicts.Count > 0) return ClassErrors.ScheduleConflict(conflicts.Select(c => c.Code).Distinct());

        db.Classes.Add(cls);
        var warnings = new List<string>();
        if (cmd.GenerateSessions)
        {
            var plan = await scheduler.ApplyAsync(cls, new ScheduleOptions(null, null, null, null, null), ct);
            if (plan.Skipped.Count > 0) warnings.Add($"Bỏ {plan.Skipped.Count} ngày lễ khi sinh lịch");
            if (!plan.HasCurriculum) warnings.Add("Lớp chưa chọn lộ trình — buổi học chưa được gán bài");
        }
        events.Publish(new Ev.ClassOpened(cls.Id.Value, cls.Code, cls.CourseId.Value, cls.BranchId.Value, cls.PlannedStartDate, cls.Fee.Amount));
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Mở lớp", cls.Code, cls.Name);
        return new ClassResult(await reader.ReloadAsync(cls.Id, ct), warnings);
    }
}
