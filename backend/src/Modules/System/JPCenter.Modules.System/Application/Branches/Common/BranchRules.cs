using JPCenter.Application.Common.Email;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Branches.Common;

/// <summary>Kiểm tra dùng chung khi thêm / sửa chi nhánh + báo đổi tài khoản ngân hàng.</summary>
public sealed class BranchRules(ISystemDbContext db, IEmployeeDirectory employees, IStaffAccessDirectory access, IEmailSender email,
    IFrontendLinks links, ICurrentUser user, IClock clock)
{
    /// <summary>CN-02.E3 (mã), CN-02.E7 (tiền tố hóa đơn) — không phân biệt hoa thường, bỏ qua chi nhánh đang sửa</summary>
    public async Task<Result> EnsureUniqueAsync(Branch branch, CancellationToken ct)
    {
        var others = db.Branches.Where(b => b.Id != branch.Id);
        if (await others.AnyAsync(b => b.Code == branch.Code, ct)) return BranchErrors.DuplicateCode(branch.Code);
        if (await others.AnyAsync(b => b.InvoicePrefix == branch.InvoicePrefix, ct)) return BranchErrors.DuplicateInvoicePrefix(branch.InvoicePrefix);
        return Result.Success();
    }

    /// <summary>CN-02.E6 — quản lý phải là nhân viên chưa nghỉ việc</summary>
    public async Task<Result> EnsureManagerAsync(Guid? managerId, CancellationToken ct)
    {
        if (managerId is not { } id) return Result.Success();
        var e = await employees.FindAsync(id, ct);
        return e is null || e.HasQuit ? BranchErrors.ManagerInvalid : Result.Success();
    }

    /// <summary>
    /// CN-04.E4 — đổi số tài khoản nhận học phí → email người có quyền xem hóa đơn ở chi nhánh đó (trừ người vừa sửa).
    /// Trả true nếu số tài khoản đã đổi.
    /// </summary>
    public async Task<bool> NotifyBankChangedAsync(Branch branch, string? oldBank, string? oldAccount, CancellationToken ct)
    {
        if (oldAccount == branch.BankAccount) return false;
        var recipients = await access.WithPermissionAsync("finance.invoices.view", branch.Id.Value, ct);
        var by = user.Access?.DisplayName ?? "Hệ thống";
        foreach (var r in recipients.Where(r => r.AccountId != user.AccountId))
        {
            await email.SendAsync(r.Email, BranchEmails.BankChanged(branch.Name, oldBank, oldAccount, branch.BankName, branch.BankAccount,
                branch.BankHolder, by, clock.UtcNow, links.Page("/admin/system/branches")), ct);
        }
        return true;
    }
}
