namespace JPCenter.Modules.Academic.Contracts.Dtos;

/// <param name="Status">"active" · "maintenance" · "inactive"</param>
public sealed record RoomSummary(Guid Id, string Name, Guid BranchId, string Status)
{
    public bool IsUsable => Status == "active";

    /// <summary>"bảo trì" / "ngừng dùng" — null khi dùng được</summary>
    public string? UnavailableLabel => Status switch { "maintenance" => "bảo trì", "inactive" => "ngừng dùng", _ => null };
}

/// <summary>1 buổi học chiếm phòng / GV (giờ địa phương)</summary>
public sealed record BusySession(Guid SessionId, string ClassCode, DateOnly Date, TimeOnly Start, TimeOnly End, Guid? RoomId,
    IReadOnlyList<Guid> TeacherIds);
