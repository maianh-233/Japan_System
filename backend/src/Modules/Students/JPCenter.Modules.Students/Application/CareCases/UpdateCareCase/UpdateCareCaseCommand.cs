using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.CareCases.Common;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Application.CareCases.UpdateCareCase;

/// <summary>
/// HV-14 bước 3 — giao người chăm sóc, đổi tiến độ (chưa liên hệ → đang liên hệ → theo dõi → ổn định), hẹn liên hệ lại.
/// Chỉ theo Id hoặc lớp × HV (chưa có trường hợp → mở mới).
/// </summary>
public sealed record UpdateCareCaseCommand(CareCaseRef Case, Guid? AssigneeId, CareStatus Status, DateOnly? NextAt) : ICommand<CareCaseDto>;

public sealed class UpdateCareCaseValidator : AbstractValidator<UpdateCareCaseCommand>
{
    public UpdateCareCaseValidator() => RuleFor(x => x.Status).IsInEnum();
}

public sealed class UpdateCareCaseHandler(IStudentsDbContext db, CareCaseOpener opener, CareCaseReader reader)
    : ICommandHandler<UpdateCareCaseCommand, CareCaseDto>
{
    public async Task<Result<CareCaseDto>> Handle(UpdateCareCaseCommand cmd, CancellationToken ct)
    {
        var found = await opener.OpenAsync(cmd.Case, ct);
        if (found.IsFailure) return found.Error;
        var c = found.Value;
        c.Assign(cmd.AssigneeId);
        c.ChangeStatus(cmd.Status, cmd.NextAt);
        await db.SaveChangesAsync(ct);
        return (await reader.ToDtosAsync([c], ct))[0];
    }
}
