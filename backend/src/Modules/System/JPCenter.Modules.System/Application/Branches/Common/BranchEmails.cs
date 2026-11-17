using JPCenter.Application.Common.Email;

namespace JPCenter.Modules.System.Application.Branches.Common;

/// <summary>Email nhóm chi nhánh.</summary>
public static class BranchEmails
{
    public static string Mask(string? account) => account is null ? "(chưa khai báo)" : "••••" + (account.Length > 4 ? account[^4..] : account);

    /// <summary>CN-04.E4 — báo người phụ trách hóa đơn khi tài khoản nhận học phí bị đổi.</summary>
    public static EmailContent BankChanged(string branchName, string? oldBank, string? oldAccount, string? newBank, string? newAccount,
        string? newHolder, string by, DateTimeOffset at, string link) =>
        EmailContent.Create($"Tài khoản nhận học phí chi nhánh {branchName} vừa thay đổi", "Tài khoản nhận học phí đã thay đổi",
                $"{by} vừa đổi tài khoản nhận học phí của chi nhánh {branchName}. Kiểm tra trước khi xuất hóa đơn.")
            .Hello(null)
            .Text($"Tài khoản ngân hàng dùng để sinh mã VietQR trên hóa đơn của chi nhánh **{branchName}** vừa được thay đổi.")
            .Details(
                ("Trước", oldAccount is null ? "(chưa khai báo)" : $"{oldBank} · {Mask(oldAccount)}"),
                ("Sau", newAccount is null ? "(đã xóa — hóa đơn không có VietQR)" : $"{newBank} · {Mask(newAccount)} · {newHolder}"),
                ("Người thay đổi", by),
                ("Thời gian", at.ToOffset(TimeSpan.FromHours(7)).ToString("HH:mm, dd/MM/yyyy")))
            .Notice("Nếu bạn **không được báo trước** về thay đổi này, hãy liên hệ quản trị viên ngay — tiền học phí có thể chuyển nhầm tài khoản.",
                EmailTone.Warning)
            .Button("Xem chi nhánh", link)
            .Footnote("Bạn nhận email này vì phụ trách hóa đơn của chi nhánh trên.");
}
