namespace JPCenter.Application.Common.Realtime;

/// <summary>
/// Đẩy sự kiện realtime tới người dùng đang mở ứng dụng (SignalR, hub <c>/hubs/realtime</c>) — dùng ở mọi module.
/// Gọi SAU khi đã lưu dữ liệu; người không trực tuyến sẽ thấy khi tải lại (không thay cho thông báo lưu DB).
/// <paramref name="type"/> = tên sự kiện client nghe, dạng <c>&lt;đối tượng&gt;.&lt;việc&gt;</c>, vd <c>substitute-invite.created</c>.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Gửi tới các nhân viên / GV (Hr.Employee) — mọi tab đang mở của họ</summary>
    Task ToEmployeesAsync(IReadOnlyCollection<Guid> employeeIds, string type, object payload, CancellationToken ct = default);

    /// <summary>Gửi tới tài khoản (user_accounts / portal_accounts) — HV, PH</summary>
    Task ToAccountsAsync(IReadOnlyCollection<Guid> accountIds, string type, object payload, CancellationToken ct = default);
}
