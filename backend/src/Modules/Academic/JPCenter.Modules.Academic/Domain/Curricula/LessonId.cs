using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Id bài học</summary>
public readonly record struct LessonId(Guid Value)
{
    public static LessonId New() => new(IdGenerator.NewId());
}
