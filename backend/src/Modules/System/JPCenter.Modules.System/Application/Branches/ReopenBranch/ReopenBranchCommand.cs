using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Branches.ReopenBranch;

/// <summary>CN-07 — mở lại chi nhánh đã đóng (giữ mã, lịch sử).</summary>
public sealed record ReopenBranchCommand(Guid Id) : ICommand<BranchStatusResult>;

public sealed class ReopenBranchHandler(ISystemDbContext db, BranchStatusEvents events, BranchReader reader, IAuditTrail audit)
    : ICommandHandler<ReopenBranchCommand, BranchStatusResult>
{
    public async Task<Result<BranchStatusResult>> Handle(ReopenBranchCommand cmd, CancellationToken ct)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == new BranchId(cmd.Id), ct);
        if (branch is null) return BranchErrors.NotFound;
        var previous = branch.Status;
        if (branch.Reopen() is { IsFailure: true } failed) return failed.Error;

        events.Changed(branch, previous);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Setting, AuditSeverity.Warning, "Mở lại chi nhánh", branch.Name, null, branchId: branch.Id.Value);
        return new BranchStatusResult(await reader.GetAsync(branch.Id, ct), []);
    }
}
