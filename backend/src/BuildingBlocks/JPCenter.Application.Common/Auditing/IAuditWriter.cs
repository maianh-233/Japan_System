namespace JPCenter.Application.Common.Auditing;

/// <summary>Hàng đợi ghi nhật ký — không làm chậm request (request-pipeline.md mục 8).</summary>
public interface IAuditWriter
{
    void Enqueue(AuditEntry entry);
}
