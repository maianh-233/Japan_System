using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.CareCases.Common;
using JPCenter.Modules.Students.Application.CareCases.UpdateCareCase;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.CareCases.AddCareLog;

/// <summary>
/// HV-14 bước 4 — "Ghi nhận liên hệ": kênh (gọi / Zalo / gặp) + nội dung* (E1) → <c>care_logs</c> (cũng hiện ở tab Học viên của lớp);
/// case "chưa liên hệ" chuyển "đang liên hệ".
/// </summary>
public sealed record AddCareLogCommand(Guid CaseId, CareChannel Channel, string Note) : ICommand<CareLogDto>;

public sealed class AddCareLogValidator : AbstractValidator<AddCareLogCommand>
{
    public AddCareLogValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class AddCareLogHandler(IStudentsDbContext db, StudentAccess access, ICurrentUser user, IClock clock)
    : ICommandHandler<AddCareLogCommand, CareLogDto>
{
    public async Task<Result<CareLogDto>> Handle(AddCareLogCommand cmd, CancellationToken ct)
    {
        var c = await db.CareCases.FirstOrDefaultAsync(x => x.Id == new CareCaseId(cmd.CaseId), ct);
        if (c is null || !access.InBranch(StudentAccess.AtRisk, PermissionAction.Update, c.BranchId.Value)) return UpdateCareCaseHandler.NotFound;
        var log = CareLog.Create(c.StudentId, c.ClassId, c.BranchId, cmd.Channel, cmd.Note?.Trim() ?? "", access.ActorId,
            user.Access?.DisplayName ?? "—", clock.UtcNow);
        if (log.IsFailure) return log.Error;
        db.CareLogs.Add(log.Value);
        c.Contacted();
        await db.SaveChangesAsync(ct);
        return CareCaseReader.LogDto(log.Value);
    }
}
