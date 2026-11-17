using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.CreateCourse;

/// <summary>KH-02 thêm khóa · KH-03 nhân bản (<paramref name="CloneFromId"/>: gắn sẵn các lộ trình chưa lưu trữ của khóa gốc, giữ mặc định)</summary>
public sealed record CreateCourseCommand(CourseInput Input, Guid? CloneFromId) : ICommand<CourseDto>;

public sealed class CreateCourseValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseValidator() => RuleFor(x => x.Input).SetValidator(new CourseInputValidator());
}

public sealed class CreateCourseHandler(IAcademicDbContext db, CourseRules rules, CourseReader reader, IAuditTrail audit, IClock clock)
    : ICommandHandler<CreateCourseCommand, CourseDto>
{
    public async Task<Result<CourseDto>> Handle(CreateCourseCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        Course? source = null;
        if (cmd.CloneFromId is { } fromId)
        {
            source = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == new CourseId(fromId), ct);
            if (source is null) return CourseErrors.NotFound;
        }
        if (await rules.EnsureCodeUniqueAsync(i.Code, null, ct) is { IsFailure: true } dup) return dup.Error;
        if (await rules.EnsureBranchesAsync(i.BranchIds, null, ct) is { IsFailure: true } branch) return branch.Error;

        var created = Course.Create(i.Code, i.Name, i.Category, i.LevelFrom, i.LevelTo, i.Format, i.TeacherType, i.Mode, i.Sessions,
            i.MinutesPerSession, i.SessionsPerWeek, Money.FromTrusted(i.Fee), i.MaxStudents, i.BranchIds.Select(b => new BranchId(b)).ToList(),
            i.Description);
        if (created.IsFailure) return created.Error;
        var course = created.Value;

        if (source is not null)
        {
            var ids = source.Curricula.Select(l => l.CurriculumId).ToList();
            var usable = await db.Curricula.Where(c => ids.Contains(c.Id) && c.Status != CurriculumStatus.Archived).Select(c => c.Id).ToListAsync(ct);
            foreach (var link in source.Curricula.Where(l => usable.Contains(l.CurriculumId)).OrderByDescending(l => l.IsDefault))
                course.LinkCurriculum(link.CurriculumId, link.IsDefault, clock.UtcNow);
        }

        db.Courses.Add(course);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, source is null ? "Thêm khóa học" : "Nhân bản khóa học", course.Name,
            source is null ? course.Code : $"{course.Code} ← {source.Code}");
        return await reader.ReloadAsync(course.Id, ct);
    }
}
