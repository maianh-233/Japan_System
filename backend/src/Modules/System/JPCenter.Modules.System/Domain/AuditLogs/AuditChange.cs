namespace JPCenter.Modules.System.Domain.AuditLogs;

/// <summary>1 trường thay đổi (lưu trong jsonb của audit_logs.changes)</summary>
public sealed record AuditChange(string Field, string? Before, string? After);
