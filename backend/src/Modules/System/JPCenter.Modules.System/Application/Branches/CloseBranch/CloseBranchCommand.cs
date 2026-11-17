using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Branches.CloseBranch;

/// <summary>CN-06 — đóng chi nhánh. Phải gõ đúng mã chi nhánh để xác nhận.</summary>
public sealed record CloseBranchCommand(Guid Id, string ConfirmCode) : ICommand<BranchStatusResult>;

public sealed class CloseBranchValidator : AbstractValidator<CloseBranchCommand>
{
    public CloseBranchValidator() => RuleFor(x => x.ConfirmCode).NotEmpty().WithMessage("Gõ mã chi nhánh để xác nhận");
}

/// <summary>
/// Gõ sai mã (E3) · còn nhân sự chưa nghỉ có chi nhánh chính là chi nhánh này (E2) · đang chuẩn bị cũng đóng được (E4).
/// ⏳ E1 (còn lớp đang học / tuyển sinh) — khi có module Đào tạo. Nhật ký mức nghiêm trọng.
/// </summary>
public sealed class CloseBranchHandler(ISystemDbContext db, IEmployeeDirectory employees, BranchStatusEvents events, BranchReader reader,
    IAuditTrail audit) : ICommandHandler<CloseBranchCommand, BranchStatusResult>
{
    public async Task<Result<BranchStatusResult>> Handle(CloseBranchCommand cmd, CancellationToken ct)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == new BranchId(cmd.Id), ct);
        if (branch is null) return BranchErrors.NotFound;
        if (!string.Equals(cmd.ConfirmCode.Trim(), branch.Code, StringComparison.OrdinalIgnoreCase)) return BranchErrors.ConfirmMismatch;

        var headcount = (await employees.HeadcountByBranchAsync(ct)).GetValueOrDefault(branch.Id.Value);
        if (headcount is { Staff: > 0 }) return BranchErrors.HasStaff(headcount.Staff);

        var previous = branch.Status;
        if (branch.Close() is { IsFailure: true } failed) return failed.Error;

        events.Changed(branch, previous);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Setting, AuditSeverity.Critical, "Đóng chi nhánh", branch.Name,
            "Không nhận lead / ghi danh / xếp lớp mới; dữ liệu cũ giữ để tra cứu", branchId: branch.Id.Value);
        return new BranchStatusResult(await reader.GetAsync(branch.Id, ct), []);
    }
}
