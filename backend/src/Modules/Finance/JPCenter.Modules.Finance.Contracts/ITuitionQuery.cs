namespace JPCenter.Modules.Finance.Contracts;

/// <summary>Tình trạng học phí cho module khác — Students tính dấu hiệu nợ quá hạn khi cảnh báo bỏ học (HV-14, TC-11.E3), xét hết nợ khi cấp chứng nhận (HV-15).</summary>
public interface ITuitionQuery
{
    /// <summary>Các HV (trong <paramref name="studentIds"/>; null = mọi HV) đang có hóa đơn còn nợ đã quá hạn tại hôm nay (giờ VN)</summary>
    Task<IReadOnlySet<Guid>> OverdueStudentsAsync(IReadOnlyCollection<Guid>? studentIds, CancellationToken ct);

    /// <summary>
    /// Học phí còn nợ (VND) theo ghi danh (Students.Enrollment) — Students xét điều kiện cấp chứng nhận "hết nợ học phí" (HV-15).
    /// Ghi danh không có hóa đơn đang mở → không có trong kết quả (= 0).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> DebtByEnrollmentAsync(IReadOnlyCollection<Guid> enrollmentIds, CancellationToken ct);
}
