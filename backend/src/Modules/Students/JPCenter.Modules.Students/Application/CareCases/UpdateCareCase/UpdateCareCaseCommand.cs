using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.CareCases.Common;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.CareCases.UpdateCareCase;

/// <summary>HV-14 bước 3 — giao người chăm sóc, đổi tiến độ (chưa liên hệ → đang liên hệ → theo dõi → ổn định), hẹn liên hệ lại</summary>
public sealed record UpdateCareCaseCommand(Guid Id, Guid? AssigneeId, CareStatus Status, DateOnly? NextAt) : ICommand<CareCaseDto>;

public sealed class UpdateCareCaseValidator : AbstractValidator<UpdateCareCaseCommand>
{
    public UpdateCareCaseValidator() => RuleFor(x => x.Status).IsInEnum();
}

public sealed class UpdateCareCaseHandler(IStudentsDbContext db, StudentAccess access, CareCaseReader reader)
    : ICommandHandler<UpdateCareCaseCommand, CareCaseDto>
{
    public static readonly Error NotFound = Error.NotFound("students.care_case.not_found", "Không tìm thấy trường hợp chăm sóc");

    public async Task<Result<CareCaseDto>> Handle(UpdateCareCaseCommand cmd, CancellationToken ct)
    {
        var c = await db.CareCases.FirstOrDefaultAsync(x => x.Id == new CareCaseId(cmd.Id), ct);
        if (c is null || !access.InBranch(StudentAccess.AtRisk, PermissionAction.Update, c.BranchId.Value)) return NotFound;
        c.Assign(cmd.AssigneeId);
        c.ChangeStatus(cmd.Status, cmd.NextAt);
        await db.SaveChangesAsync(ct);
        return (await reader.ToDtosAsync([c], ct))[0];
    }
}
