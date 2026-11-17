using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Branches.UpdateBranch;

/// <summary>
/// CN-03: trùng tiền tố (CN-02.E7) · sửa đè (E3, xmin) · đổi quản lý chỉ đổi hiển thị, không tự phân quyền (E4).
/// CN-04: đổi số tài khoản → nhật ký mức cảnh báo + email người phụ trách hóa đơn chi nhánh (E4).
/// ⏳ CN-03.E2 (khóa tiền tố khi đã có hóa đơn) — khi có module Tài chính.
/// </summary>
public sealed class UpdateBranchHandler(ISystemDbContext db, BranchRules rules, BranchReader reader, IAuditTrail audit)
    : ICommandHandler<UpdateBranchCommand, BranchDto>
{
    public async Task<Result<BranchDto>> Handle(UpdateBranchCommand cmd, CancellationToken ct)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == new BranchId(cmd.Id), ct);
        if (branch is null) return BranchErrors.NotFound;
        ((DbContext)db).Entry(branch).Property("xmin").OriginalValue = cmd.Version;

        var (oldBank, oldAccount) = (branch.BankName, branch.BankAccount);
        if (branch.Update(cmd.Name, cmd.Address, cmd.Phone, cmd.Email, cmd.InvoicePrefix, cmd.OpenedAt) is { IsFailure: true } invalid)
            return invalid.Error;
        if (await rules.EnsureUniqueAsync(branch, ct) is { IsFailure: true } dup) return dup.Error;
        if (await rules.EnsureManagerAsync(cmd.ManagerId, ct) is { IsFailure: true } mgr) return mgr.Error;
        if (branch.SetBankAccount(cmd.Bank?.Bank, cmd.Bank?.Account, cmd.Bank?.Holder) is { IsFailure: true } bank) return bank.Error;
        branch.AssignManager(cmd.ManagerId);
        branch.SetDetails(cmd.OpenHours, cmd.Note);

        await db.SaveChangesAsync(ct);
        var bankChanged = await rules.NotifyBankChangedAsync(branch, oldBank, oldAccount, ct);
        audit.Record(AuditCategory.Setting, bankChanged ? AuditSeverity.Warning : AuditSeverity.Info, "Sửa chi nhánh", branch.Name,
            bankChanged ? $"Đổi tài khoản nhận học phí: {BranchEmails.Mask(oldAccount)} → {BranchEmails.Mask(branch.BankAccount)}" : null,
            branchId: branch.Id.Value);
        return await reader.GetAsync(branch.Id, ct);
    }
}
