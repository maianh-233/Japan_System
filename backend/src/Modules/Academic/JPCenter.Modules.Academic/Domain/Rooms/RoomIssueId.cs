using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Rooms;

/// <summary>Id báo hư hỏng</summary>
public readonly record struct RoomIssueId(Guid Value)
{
    public static RoomIssueId New() => new(IdGenerator.NewId());
}
