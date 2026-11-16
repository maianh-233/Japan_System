using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>Id ứng viên</summary>
public readonly record struct CandidateId(Guid Value)
{
    public static CandidateId New() => new(IdGenerator.NewId());
}
