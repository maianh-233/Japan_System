using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.AuditLogs;

/// <summary>Id dòng nhật ký hệ thống</summary>
public readonly record struct AuditLogId(Guid Value)
{
    public static AuditLogId New() => new(IdGenerator.NewId());
}
