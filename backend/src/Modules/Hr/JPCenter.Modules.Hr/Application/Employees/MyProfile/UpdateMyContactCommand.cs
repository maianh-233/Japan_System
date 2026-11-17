using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Employees.MyProfile;

/// <summary>NS-11 — tự sửa SĐT, ngày sinh. Không kiểm sửa đè (E6): chỉ 2 trường này, ghi sau thắng.</summary>
public sealed record UpdateMyContactCommand(string Phone, DateOnly? Dob) : ICommand;

public sealed class UpdateMyContactValidator : AbstractValidator<UpdateMyContactCommand>
{
    public UpdateMyContactValidator() =>
        RuleFor(x => x.Phone).Matches(@"^0\d{9}$").WithErrorCode("hr.employee.invalid_phone").WithMessage("SĐT 10 số, bắt đầu bằng 0");
}

public sealed class UpdateMyContactHandler(IHrDbContext db, ICurrentUser user, IAuditTrail audit, IClock clock)
    : ICommandHandler<UpdateMyContactCommand>
{
    public async Task<Result> Handle(UpdateMyContactCommand cmd, CancellationToken ct)
    {
        if (user.Access?.EmployeeId is not { } me) return EmployeeErrors.NotFound;
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == new EmployeeId(me), ct);
        if (e is null) return EmployeeErrors.NotFound;
        if (e.UpdateOwnContact(cmd.Phone, cmd.Dob, clock.Today) is { IsFailure: true } failed) return failed;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Tự cập nhật liên hệ", e.FullName, e.Code, branchId: e.BranchId.Value);
        return Result.Success();
    }
}
