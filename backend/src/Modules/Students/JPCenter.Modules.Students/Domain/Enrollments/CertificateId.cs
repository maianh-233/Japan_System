using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>Id chứng nhận hoàn thành khóa</summary>
public readonly record struct CertificateId(Guid Value)
{
    public static CertificateId New() => new(IdGenerator.NewId());
}
