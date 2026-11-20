using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.ClassSessions.AddSupportEntry;

/// <summary>Nhật ký hỗ trợ của nhân viên trong buổi (phòng / thiết bị, HV về sớm, khác…)</summary>
public sealed record AddSupportEntryCommand(Guid Id, SupportKind Kind, string Note) : ICommand<SessionResult>;

public sealed class AddSupportEntryValidator : AbstractValidator<AddSupportEntryCommand>
{
    public AddSupportEntryValidator() => RuleFor(x => x.Note).NotEmpty().WithMessage("Nhập nội dung").MaximumLength(1000);
}

public sealed class AddSupportEntryHandler(IAcademicDbContext db, SessionReader reader, AcademicAccess access, IClock clock)
    : ICommandHandler<AddSupportEntryCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(AddSupportEntryCommand cmd, CancellationToken ct)
    {
        if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct) is not var (session, _)) return SessionErrors.NotFound;
        if (session.Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        session.AddSupport(cmd.Kind, cmd.Note, access.ActorId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), []);
    }
}
