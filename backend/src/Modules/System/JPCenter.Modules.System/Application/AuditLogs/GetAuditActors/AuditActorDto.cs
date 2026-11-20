namespace JPCenter.Modules.System.Application.AuditLogs.GetAuditActors;

/// <summary>Người có trong nhật ký — ô lọc "Người thực hiện"</summary>
public sealed record AuditActorDto(Guid Id, string Name, string Role);
