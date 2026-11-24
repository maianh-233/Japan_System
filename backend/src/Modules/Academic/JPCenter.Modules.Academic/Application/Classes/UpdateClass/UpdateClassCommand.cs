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
namespace JPCenter.Modules.Academic.Application.Classes.UpdateClass;

/// <summary>
/// DT-07 — sửa lớp (sửa đè → 409). Lớp đã khai giảng khóa khóa học / lộ trình / mã / chi nhánh; đã đóng chỉ sửa ghi chú.
/// <paramref name="ApplyToUpcoming"/>: đổi giờ học → áp cho buổi chưa dạy không có thay đổi riêng (DT-07.E4).
/// GV / phòng của lớp luôn áp cho buổi không ghi đè; đổi thứ học → cần "Tạo lịch tự động" lại.
/// </summary>
public sealed record UpdateClassCommand(Guid Id, ClassInput Input, bool ApplyToUpcoming, uint RowVersion) : ICommand<ClassResult>;

public sealed class UpdateClassValidator : AbstractValidator<UpdateClassCommand>
{
    public UpdateClassValidator() => RuleFor(x => x.Input).SetValidator(new ClassInputValidator());
}

public sealed class UpdateClassHandler(IAcademicDbContext db, ClassRules rules, ClassReader reader, ClassRoster roster, AcademicAccess access,
    IClock clock, IAuditTrail audit, ClassMeetingRooms rooms) : ICommandHandler<UpdateClassCommand, ClassResult>
{
    public async Task<Result<ClassResult>> Handle(UpdateClassCommand cmd, CancellationToken ct)
    {
        var cls = await reader.ForUpdateAsync(cmd.Id, AcademicAccess.Classes, PermissionAction.Update, ct);
        if (cls is null) return ClassErrors.NotFound;
        ((DbContext)db).Entry(cls).Property("xmin").OriginalValue = cmd.RowVersion;
        var i = cmd.Input;
        if (i.BranchId != cls.BranchId.Value && !access.InBranch(AcademicAccess.Classes, PermissionAction.Update, i.BranchId))
            return AcademicAccess.BranchNotAllowed;

        var course = await rules.CourseAsync(i.CourseId, i.BranchId, cls, ct);
        if (course.IsFailure) return course.Error;
        var setup = i.ToSetup(i.PlannedEndDate ?? cls.PlannedEndDate);
        if (!cls.IsClosed)
        {
            if (await rules.EnsureBranchAsync(i.BranchId, cls, ct) is { IsFailure: true } branch) return branch.Error;
            if (await rules.EnsureCurriculumAsync(i.CurriculumId, course.Value, cls, ct) is { IsFailure: true } cur) return cur.Error;
            var code = Class.NormalizeCode(i.Code, course.Value.TeacherType);
            if (code.IsFailure) return code.Error;
            if (await rules.EnsureCodeUniqueAsync(code.Value, cls.Id, ct) is { IsFailure: true } dup) return dup.Error;
            if (await rules.EnsureStaffAsync(setup.TeacherIds.Concat(setup.AssistantIds), cls, ct) is { IsFailure: true } staff) return staff.Error;
        }
        var capacity = cls.IsClosed ? Result.Success<int?>(null) : await rules.RoomCapacityAsync(i.RoomId, i.BranchId, i.Online, cls, ct);
        if (capacity.IsFailure) return capacity.Error;

        var scheduleChanged = cls.ScheduleDiffers(setup) || setup.PlannedStartDate != cls.PlannedStartDate || setup.PlannedEndDate != cls.PlannedEndDate;
        var oldWeekdays = cls.Weekdays.ToList();
        var (oldStart, oldEnd) = (cls.StartTime, cls.EndTime);
        var changed = cls.Update(course.Value.Id, i.CurriculumId is { } cid ? new Domain.Curricula.CurriculumId(cid) : null, course.Value.TeacherType,
            setup, capacity.Value, await roster.EnrolledAsync(cls.Id.Value, ct) ?? 0);
        if (changed.IsFailure) return changed.Error;

        if (scheduleChanged && !cls.IsClosed)
        {
            var conflicts = await rules.ConflictsAsync(new ClassScheduleProbe(cls.TeacherIds.Concat(cls.AssistantIds).ToList(), cls.RoomId?.Value,
                cls.ActualStartDate ?? cls.PlannedStartDate, cls.PlannedEndDate, cls.Weekdays, cls.StartTime, cls.EndTime), cls.Id, ct);
            if (conflicts.Count > 0) return ClassErrors.ScheduleConflict(conflicts.Select(c => c.Code).Distinct());
        }

        var warnings = new List<string>();
        if (cmd.ApplyToUpcoming && (cls.StartTime != oldStart || cls.EndTime != oldEnd))
        {
            var now = clock.LocalNow;
            var upcoming = (await db.ClassSessions.AsSplitQuery().Where(s => s.ClassId == cls.Id && s.Status == Domain.ClassSessions.SessionStatus.Scheduled
                && s.ConfirmedAt == null).ToListAsync(ct)).Where(s => s.StartsAt > now && !s.HasOverrides).ToList();
            foreach (var s in upcoming) s.FollowClass(cls.StartTime, cls.EndTime);
            warnings.Add($"Đã đổi giờ {upcoming.Count} buổi chưa dạy theo lịch mới");
        }
        if (!oldWeekdays.SequenceEqual(cls.Weekdays))
            warnings.Add("Đã đổi thứ học — vào tab Buổi học bấm \"Tạo lịch tự động\" để sinh lại các buổi chưa dạy");

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Sửa lớp", cls.Code, cls.Name);
        // TH-08 — lớp online chưa có link: tự tạo phòng (lỗi → lớp vẫn lưu, chỉ cảnh báo)
        if (await rooms.EnsureAsync(cls, ct) is { } roomWarning) warnings.Add(roomWarning);
        return new ClassResult(await reader.ReloadAsync(cls.Id, ct), warnings);
    }
}
