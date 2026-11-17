using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Branches.CreateBranch;

/// <summary>CN-02: trùng mã (E3) · trùng tiền tố hóa đơn (E7) · quản lý không hợp lệ (E6) · tài khoản ngân hàng thiếu (CN-04.E1).</summary>
public sealed class CreateBranchHandler(ISystemDbContext db, BranchRules rules, BranchReader reader, IAuditTrail audit)
    : ICommandHandler<CreateBranchCommand, BranchDto>
{
    public async Task<Result<BranchDto>> Handle(CreateBranchCommand cmd, CancellationToken ct)
    {
        var created = Branch.Create(cmd.Code, cmd.Name, cmd.Address, cmd.Phone, cmd.Email, cmd.InvoicePrefix, cmd.OpenedAt);
        if (created.IsFailure) return created.Error;
        var branch = created.Value;

        if (await rules.EnsureUniqueAsync(branch, ct) is { IsFailure: true } dup) return dup.Error;
        if (await rules.EnsureManagerAsync(cmd.ManagerId, ct) is { IsFailure: true } mgr) return mgr.Error;
        if (branch.SetBankAccount(cmd.Bank?.Bank, cmd.Bank?.Account, cmd.Bank?.Holder) is { IsFailure: true } bank) return bank.Error;
        branch.AssignManager(cmd.ManagerId);
        branch.SetDetails(cmd.OpenHours, cmd.Note);

        db.Branches.Add(branch);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Setting, AuditSeverity.Info, "Thêm chi nhánh", branch.Name, $"Mã {branch.Code} · đang chuẩn bị",
            branchId: branch.Id.Value);
        return await reader.GetAsync(branch.Id, ct);
    }
}
