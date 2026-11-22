using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Application.TeacherReviews.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.TeacherReviews.SubmitTeacherReview;

/// <summary>
/// ND-08 — HV đánh giá GV của lớp đang học trong đợt khảo sát đang mở (E3): tổng + 4 tiêu chí 1–5 sao (E2), góp ý, ẩn danh.
/// Mỗi HV × GV × đợt 1 lần (E1). GV không dạy lớp → 403.
/// </summary>
public sealed record SubmitTeacherReviewCommand(Guid ClassId, Guid TeacherId, int Overall, ReviewRatingsDto Ratings, string? Comment, bool Anonymous)
    : ICommand<TeacherReviewDto>;

public sealed class SubmitTeacherReviewValidator : AbstractValidator<SubmitTeacherReviewCommand>
{
    public SubmitTeacherReviewValidator() => RuleFor(x => x.Ratings).NotNull();
}

public sealed class SubmitTeacherReviewHandler(IAcademicDbContext db, LearningAccess access, TeacherReviewReader reader, IClock clock)
    : ICommandHandler<SubmitTeacherReviewCommand, TeacherReviewDto>
{
    public async Task<Result<TeacherReviewDto>> Handle(SubmitTeacherReviewCommand cmd, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(null, ct);
        if (learner.IsFailure) return learner.Error;
        if (!learner.Value.Studies(cmd.ClassId)) return LearningAccess.NotInClass;
        var cls = await db.Classes.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == new ClassId(cmd.ClassId), ct);
        if (cls is null) return ClassErrors.NotFound;
        if (!cls.TeacherIds.Contains(cmd.TeacherId)) return TeacherReviewErrors.NotYourTeacher;
        if (await reader.OpenPeriodAsync(cls, clock.Today, ct) is not { } period) return TeacherReviewErrors.NotOpen;

        var me = learner.Value.StudentId;
        if (await db.TeacherReviews.AnyAsync(r => r.ClassId == cls.Id && r.TeacherId == cmd.TeacherId && r.StudentId == me && r.Period == period, ct))
            return TeacherReviewErrors.Duplicate;
        var r = cmd.Ratings;
        var created = TeacherReview.Create(cls.Id, cmd.TeacherId, me, period, cmd.Overall, r.Method, r.Clarity, r.Enthusiasm, r.Punctuality, cmd.Comment,
            cmd.Anonymous, clock.UtcNow);
        if (created.IsFailure) return created.Error;
        db.TeacherReviews.Add(created.Value);
        await db.SaveChangesAsync(ct);
        return (await reader.ToDtosAsync([created.Value], hideAnonymous: false, ct))[0];
    }
}
