using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.UpdateCourse;

/// <summary>KH-04 — sửa khóa. Đã có lớp → khóa mã (E1), hình thức + loại GV (E2). Sửa đè → 409 (E4, xmin).</summary>
public sealed record UpdateCourseCommand(Guid Id, CourseInput Input, uint RowVersion) : ICommand<CourseDto>;

public sealed class UpdateCourseValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseValidator() => RuleFor(x => x.Input).SetValidator(new CourseInputValidator());
}

public sealed class UpdateCourseHandler(IAcademicDbContext db, CourseRules rules, ClassUsage classes, CourseReader reader, IAuditTrail audit)
    : ICommandHandler<UpdateCourseCommand, CourseDto>
{
    public async Task<Result<CourseDto>> Handle(UpdateCourseCommand cmd, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == new CourseId(cmd.Id), ct);
        if (course is null) return CourseErrors.NotFound;
        ((DbContext)db).Entry(course).Property("xmin").OriginalValue = cmd.RowVersion;

        var i = cmd.Input;
        if (await rules.EnsureCodeUniqueAsync(i.Code, course.Id, ct) is { IsFailure: true } dup) return dup.Error;
        if (await rules.EnsureBranchesAsync(i.BranchIds, course.BranchIds.Select(b => b.Value), ct) is { IsFailure: true } branch) return branch.Error;

        var changed = course.Update(i.Code, i.Name, i.Category, i.LevelFrom, i.LevelTo, i.Format, i.TeacherType, i.Mode, i.Sessions,
            i.MinutesPerSession, i.SessionsPerWeek, Money.FromTrusted(i.Fee), i.MaxStudents, i.BranchIds.Select(b => new BranchId(b)).ToList(),
            i.Description, await classes.CourseHasClassesAsync(cmd.Id, ct));
        if (changed.IsFailure) return changed.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Sửa khóa học", course.Name, course.Code);
        return await reader.ReloadAsync(course.Id, ct);
    }
}
