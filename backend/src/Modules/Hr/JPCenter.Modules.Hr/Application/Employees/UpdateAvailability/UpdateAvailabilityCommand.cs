using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.UpdateAvailability;

/// <summary>
/// NS-08 — lịch rảnh giáo viên: gửi CẢ bảng 1 lần (thứ 1 = T2 … 7 = CN → ca). Frontend gom nhiều lần bấm thành 1 lần lưu (E3).
/// ⏳ E2 (tắt ca đang có lớp cố định) — chờ module Đào tạo.
/// </summary>
public sealed record UpdateAvailabilityCommand(Guid Id, Dictionary<int, List<ShiftKey>> Availability) : ICommand<EmployeeDto>;

public sealed class UpdateAvailabilityValidator : AbstractValidator<UpdateAvailabilityCommand>
{
    public UpdateAvailabilityValidator() =>
        RuleFor(x => x.Availability).NotNull().Must(a => a.Keys.All(k => k is >= 1 and <= 7)).WithMessage("Thứ phải từ 1 (T2) đến 7 (CN)");
}

public sealed class UpdateAvailabilityHandler(IHrDbContext db, EmployeeAccess access, IAuditTrail audit)
    : ICommandHandler<UpdateAvailabilityCommand, EmployeeDto>
{
    public async Task<Result<EmployeeDto>> Handle(UpdateAvailabilityCommand cmd, CancellationToken ct)
    {
        var e = await access.FindForUpdateAsync(cmd.Id, EmployeeGroup.Teaching, ct);
        if (e is null) return EmployeeErrors.NotFound;
        if (e.SetAvailability(cmd.Availability) is { IsFailure: true } failed) return failed.Error;

        await db.SaveChangesAsync(ct);
        var slots = cmd.Availability.Sum(kv => kv.Value.Distinct().Count());
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Cập nhật lịch rảnh", e.FullName, $"{slots} ca rảnh / tuần", branchId: e.BranchId.Value);
        return await access.DetailAsync(e.Id, EmployeeGroup.Teaching, ct);
    }
}
