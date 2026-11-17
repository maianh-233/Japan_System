using JPCenter.Application.Common.Email;

namespace JPCenter.Modules.Hr.Application.Employees.Common;

/// <summary>Email nhóm nhân sự (khuôn thương hiệu chung — BuildingBlocks).</summary>
public static class EmployeeEmails
{
    /// <param name="Name">Họ tên · mã</param>
    /// <param name="Detail">Vị trí · chi nhánh</param>
    public sealed record ExpiringItem(string Name, string Detail, DateOnly End, int DaysLeft, string Link);

    /// <summary>NS-09.E1 — hợp đồng còn 45 / 15 / 7 ngày</summary>
    public static EmailContent ContractsExpiring(string? recipientName, IReadOnlyList<ExpiringItem> items, string listLink)
    {
        var soonest = items.Min(i => i.DaysLeft);
        var content = EmailContent.Create(
                items.Count == 1 ? $"Hợp đồng của {items[0].Name.Split(" · ")[0]} còn {items[0].DaysLeft} ngày" : $"{items.Count} hợp đồng lao động sắp hết hạn",
                "Hợp đồng lao động sắp hết hạn",
                $"Gần nhất còn {soonest} ngày — gia hạn hoặc ký mới để nhân sự không làm việc khi hợp đồng đã hết.")
            .Hello(recipientName)
            .Text("Các hợp đồng lao động dưới đây sắp hết hạn. Hãy **gia hạn / ký hợp đồng mới** (hoặc cho nghỉ việc) trước ngày hết hạn:")
            .Items(items.OrderBy(i => i.DaysLeft).Select(i => $"**{i.Name}** — {i.Detail} · hết hạn {i.End:dd/MM/yyyy} (còn {i.DaysLeft} ngày)"));
        if (soonest <= 7) content.Notice($"Có hợp đồng chỉ còn **{soonest} ngày**. Ưu tiên xử lý ngay.", EmailTone.Warning);
        return content
            .Button(items.Count == 1 ? "Mở hồ sơ" : "Xem danh sách hợp đồng sắp hết hạn", items.Count == 1 ? items[0].Link : listLink)
            .Footnote("Hệ thống nhắc khi hợp đồng còn 45, 15 và 7 ngày. Bạn nhận email này vì phụ trách nhân sự của chi nhánh hoặc là quản lý trực tiếp.");
    }
}
