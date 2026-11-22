namespace JPCenter.Modules.Finance.Contracts;

/// <summary>Tình trạng học phí cho module khác — Students tính dấu hiệu nợ quá hạn khi cảnh báo bỏ học (HV-14, TC-11.E3).</summary>
public interface ITuitionQuery
{
    /// <summary>Các HV (trong <paramref name="studentIds"/>; null = mọi HV) đang có hóa đơn còn nợ đã quá hạn tại hôm nay (giờ VN)</summary>
    Task<IReadOnlySet<Guid>> OverdueStudentsAsync(IReadOnlyCollection<Guid>? studentIds, CancellationToken ct);
}
