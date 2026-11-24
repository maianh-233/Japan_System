namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>Phòng học online vừa tạo (TH-08). <paramref name="Url"/> null = không tạo được (lớp hiện "Chưa có link phòng").</summary>
/// <param name="Provider">"zoom" · "meet"</param>
public sealed record MeetingRoom(string? Url, string? Provider, string Detail);
