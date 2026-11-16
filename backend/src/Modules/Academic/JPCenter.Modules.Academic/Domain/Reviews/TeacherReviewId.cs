using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Reviews;

/// <summary>Id đánh giá giáo viên</summary>
public readonly record struct TeacherReviewId(Guid Value)
{
    public static TeacherReviewId New() => new(IdGenerator.NewId());
}
