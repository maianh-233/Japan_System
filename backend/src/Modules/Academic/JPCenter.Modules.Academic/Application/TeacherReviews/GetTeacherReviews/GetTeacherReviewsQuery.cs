using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Application.TeacherReviews.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.TeacherReviews.GetTeacherReviews;

/// <summary>
/// ND-08 — đánh giá GV. <paramref name="ClassId"/> có: tab Đánh giá GV của lớp (nhân viên <c>academic.classes.view</c> — thấy cả tên HV ẩn danh).
/// <paramref name="ClassId"/> null + cổng GV: đánh giá về chính GV đang gọi, ẩn danh → không trả tên / Id HV (E4).
/// </summary>
public sealed record GetTeacherReviewsQuery(Guid? ClassId, bool AsTeacher) : IQuery<IReadOnlyList<TeacherReviewDto>>;

public sealed class GetTeacherReviewsHandler(IAcademicDbContext db, LearningAccess access, TeacherReviewReader reader)
    : IQueryHandler<GetTeacherReviewsQuery, IReadOnlyList<TeacherReviewDto>>
{
    public async Task<Result<IReadOnlyList<TeacherReviewDto>>> Handle(GetTeacherReviewsQuery q, CancellationToken ct)
    {
        if (q.AsTeacher)
        {
            if (access.TeacherId is not { } me) return LearningAccess.NotYourClass;
            var mine = await db.TeacherReviews.AsNoTracking().Where(r => r.TeacherId == me).ToListAsync(ct);
            return Result.Success(await reader.ToDtosAsync(mine, hideAnonymous: true, ct));
        }
        if (q.ClassId is not { } classId) return LearningAccess.NotYourClass;
        var cls = await access.ClassAsync(classId, false, PermissionAction.View, ct);
        if (cls.IsFailure) return cls.Error;
        var list = await db.TeacherReviews.AsNoTracking().Where(r => r.ClassId == cls.Value.Id).ToListAsync(ct);
        return Result.Success(await reader.ToDtosAsync(list, hideAnonymous: false, ct));
    }
}
