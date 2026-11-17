using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Branches.OpenBranch;

/// <summary>CN-05 — khai trương: đang chuẩn bị → hoạt động.</summary>
public sealed record OpenBranchCommand(Guid Id) : ICommand<BranchStatusResult>;

/// <summary>Đã hoạt động (E1) · khai trương sớm → ngày khai trương = hôm nay (E2) · thiếu ngân hàng / quản lý → cảnh báo, vẫn cho (E3).</summary>
public sealed class OpenBranchHandler(ISystemDbContext db, BranchStatusEvents events, BranchReader reader, IAuditTrail audit, IClock clock)
    : ICommandHandler<OpenBranchCommand, BranchStatusResult>
{
    public async Task<Result<BranchStatusResult>> Handle(OpenBranchCommand cmd, CancellationToken ct)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == new BranchId(cmd.Id), ct);
        if (branch is null) return BranchErrors.NotFound;
        var previous = branch.Status;
        if (branch.Open(clock.Today) is { IsFailure: true } failed) return failed.Error;

        events.Changed(branch, previous);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Setting, AuditSeverity.Warning, "Khai trương chi nhánh", branch.Name, null, branchId: branch.Id.Value);

        var dto = await reader.GetAsync(branch.Id, ct);
        var warnings = new List<string>();
        if (dto.Bank is null) warnings.Add("Chưa khai báo tài khoản nhận học phí — hóa đơn chưa có mã VietQR.");
        if (dto.ManagerId is null) warnings.Add("Chưa có quản lý chi nhánh.");
        return new BranchStatusResult(dto, warnings);
    }
}
