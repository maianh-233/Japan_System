using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Rooms.Common;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
namespace JPCenter.Modules.Academic.Application.Rooms.ReportRoomIssue;

/// <summary>Báo hư hỏng phòng / thiết bị — ai xem được phòng đều báo được</summary>
public sealed record ReportRoomIssueCommand(Guid RoomId, Equipment? Equipment, string Title, string? Description, IssueSeverity Severity)
    : ICommand<RoomIssueDto>;

public sealed class ReportRoomIssueValidator : AbstractValidator<ReportRoomIssueCommand>
{
    public ReportRoomIssueValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập nội dung hư hỏng").MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public sealed class ReportRoomIssueHandler(IAcademicDbContext db, RoomReader reader, AcademicAccess access, IClock clock, IAuditTrail audit)
    : ICommandHandler<ReportRoomIssueCommand, RoomIssueDto>
{
    public async Task<Result<RoomIssueDto>> Handle(ReportRoomIssueCommand cmd, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == new RoomId(cmd.RoomId), ct);
        if (room is null || !access.InBranch(AcademicAccess.Rooms, PermissionAction.View, room.BranchId.Value)) return RoomErrors.NotFound;
        var issue = RoomIssue.Report(room.Id, cmd.Equipment, cmd.Title.Trim(), string.IsNullOrWhiteSpace(cmd.Description) ? null : cmd.Description.Trim(),
            cmd.Severity, access.ActorId, clock.UtcNow);
        db.RoomIssues.Add(issue);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, cmd.Severity == IssueSeverity.High ? AuditSeverity.Warning : AuditSeverity.Info,
            "Báo hư hỏng phòng", room.Name, issue.Title);
        return await reader.IssueAsync(issue.Id, ct);
    }
}
