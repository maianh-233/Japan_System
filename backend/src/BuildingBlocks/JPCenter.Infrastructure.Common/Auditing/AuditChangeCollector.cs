using JPCenter.Application.Common.Auditing;

namespace JPCenter.Infrastructure.Common.Auditing;

/// <summary>Gom thay đổi dữ liệu trong 1 request (scoped). Giới hạn 100 dòng để nhật ký không phình.</summary>
public sealed class AuditChangeCollector : IAuditChangeCollector
{
    private const int Max = 100;
    private readonly List<AuditFieldChange> _changes = [];

    public void Add(AuditFieldChange change)
    {
        if (_changes.Count < Max) _changes.Add(change);
    }

    public IReadOnlyList<AuditFieldChange> Drain()
    {
        var copy = _changes.ToList();
        _changes.Clear();
        return copy;
    }
}
