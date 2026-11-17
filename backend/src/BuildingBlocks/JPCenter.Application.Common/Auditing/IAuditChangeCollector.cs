namespace JPCenter.Application.Common.Auditing;

/// <summary>Gom các thay đổi dữ liệu (EF) trong 1 request để gắn vào dòng nhật ký của hành động đó.</summary>
public interface IAuditChangeCollector
{
    void Add(AuditFieldChange change);

    /// <summary>Lấy ra và xóa danh sách đã gom.</summary>
    IReadOnlyList<AuditFieldChange> Drain();
}
