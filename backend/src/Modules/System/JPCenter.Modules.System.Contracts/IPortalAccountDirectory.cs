namespace JPCenter.Modules.System.Contracts;

/// <summary>
/// Tài khoản cổng đang hoạt động của HV / PH — module khác gửi thông báo chuông tới cổng (vd bài tập mới, kết quả thi, thông báo lớp — user story 12).
/// Chưa cấp tài khoản / tài khoản ngừng / khóa → bỏ qua.
/// </summary>
public interface IPortalAccountDirectory
{
    /// <param name="studentIds">Students.Student — tài khoản cổng HV</param>
    /// <param name="guardianLeadIds">Crm.Lead — tài khoản cổng PH</param>
    /// <returns>Id tài khoản cổng (System.PortalAccount), không trùng</returns>
    Task<IReadOnlyList<Guid>> ActiveAccountsAsync(IReadOnlyCollection<Guid> studentIds, IReadOnlyCollection<Guid> guardianLeadIds, CancellationToken ct);
}
