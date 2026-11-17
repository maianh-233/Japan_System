namespace JPCenter.Application.Common.Auditing;

/// <summary>Nơi lưu nhật ký thật — module System hiện thực (bảng system.audit_logs). Mặc định: chỉ ghi log.</summary>
public interface IAuditSink
{
    Task WriteAsync(IReadOnlyList<AuditEntry> entries, CancellationToken ct);
}
