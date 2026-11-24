using JPCenter.Modules.Hr.Contracts.Dtos;

namespace JPCenter.Modules.Hr.Contracts;

/// <summary>Đơn từ đến lượt <b>người đang đăng nhập</b> duyệt — cùng quy tắc tab "Chờ tôi duyệt" (DN-02) — cho Việc hôm nay (user story 17).</summary>
public interface IHrRequestInbox
{
    /// <summary><paramref name="branchIds"/> null = mọi chi nhánh</summary>
    Task<IReadOnlyList<PendingHrRequest>> PendingForMeAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct);
}
