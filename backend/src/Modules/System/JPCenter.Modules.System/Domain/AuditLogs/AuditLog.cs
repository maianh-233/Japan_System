using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.AuditLogs;

/// <summary>Nhật ký hệ thống: ai làm gì lúc nào (chỉ thêm, không sửa / xóa). Bảng: system.audit_logs</summary>
public sealed class AuditLog : Entity<AuditLogId>
{
    private AuditLog() { }

    public DateTimeOffset At { get; private set; }
    public Guid? ActorId { get; private set; }
    public string ActorName { get; private set; } = null!;
    public string ActorRole { get; private set; } = null!;
    public BranchId? BranchId { get; private set; }
    public AuditCategory Category { get; private set; }
    public AuditSeverity Severity { get; private set; }
    /// <summary>Hành động ngắn: "Cấp quyền", "Xóa học viên"</summary>
    public string Action { get; private set; } = null!;
    /// <summary>Đối tượng bị tác động</summary>
    public string Target { get; private set; } = null!;
    public string? Detail { get; private set; }
    /// <summary>Danh sách thay đổi trước → sau (jsonb)</summary>
    public List<AuditChange> Changes { get; private set; } = [];
    public string Ip { get; private set; } = null!;
    public string Device { get; private set; } = null!;
    /// <summary>Mã truy vết request (X-Request-Id) — nối với log kỹ thuật</summary>
    public string? TraceId { get; private set; }

    public static AuditLog Create(DateTimeOffset at, Guid? actorId, string actorName, string actorRole, BranchId? branchId,
        AuditCategory category, AuditSeverity severity, string action, string target, string? detail,
        List<AuditChange> changes, string ip, string device, string? traceId = null) =>
        new()
        {
            Id = AuditLogId.New(), At = at, ActorId = actorId, ActorName = actorName, ActorRole = actorRole, BranchId = branchId,
            Category = category, Severity = severity, Action = action, Target = target, Detail = detail, Changes = changes, Ip = ip, Device = device, TraceId = traceId,
        };
}
