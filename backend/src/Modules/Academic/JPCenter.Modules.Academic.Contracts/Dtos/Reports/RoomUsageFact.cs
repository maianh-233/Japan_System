namespace JPCenter.Modules.Academic.Contracts.Dtos.Reports;

/// <summary>Số phút có buổi học (không hủy, không online) trong phòng trong khoảng ngày — BC-05 bước 5.</summary>
public sealed record RoomUsageFact(Guid RoomId, string Name, Guid BranchId, int Minutes);
