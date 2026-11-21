namespace JPCenter.Modules.Crm.Domain.Appointments;

/// <summary>
/// Khoảng bận ngoài Crm (đọc qua Academic.Contracts): buổi học dùng phòng, hoặc giờ dạy của người tiếp (GV).
/// <paramref name="Label"/> mô tả sẵn, vd "lớp N3K12 học 18:00–20:00".
/// </summary>
public sealed record BusySlot(DateTimeOffset Start, DateTimeOffset End, string Label);
