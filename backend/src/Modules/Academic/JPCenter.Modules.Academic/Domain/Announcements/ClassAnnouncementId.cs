using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Announcements;

/// <summary>Id thông báo lớp</summary>
public readonly record struct ClassAnnouncementId(Guid Value)
{
    public static ClassAnnouncementId New() => new(IdGenerator.NewId());
}
