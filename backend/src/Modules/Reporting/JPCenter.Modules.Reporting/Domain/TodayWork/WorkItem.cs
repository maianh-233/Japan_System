namespace JPCenter.Modules.Reporting.Domain.TodayWork;

/// <summary>1 việc cụ thể — bấm vào mở đúng bản ghi (<paramref name="Path"/>)</summary>
/// <param name="Key">Khóa duy nhất trong bảng, vd <c>sla:{leadId}</c></param>
/// <param name="At">Mốc thời gian để xếp cùng mức độ (cũ trước)</param>
public sealed record WorkItem(string Key, string Title, string? Subtitle, WorkUrgency Urgency, string Path, DateTime At)
{
    /// <summary>Trong mỗi nhóm: đỏ → vàng → xanh, cùng mức thì cũ trước (BC-02)</summary>
    public static IReadOnlyList<WorkItem> Order(IEnumerable<WorkItem> items) =>
        items.OrderBy(i => i.Urgency.Tone).ThenBy(i => i.At).ThenBy(i => i.Key, StringComparer.Ordinal).ToList();
}
