using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Rooms;

/// <summary>Id phòng học</summary>
public readonly record struct RoomId(Guid Value)
{
    public static RoomId New() => new(IdGenerator.NewId());
}
