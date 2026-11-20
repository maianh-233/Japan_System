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
namespace JPCenter.Modules.Academic.Application.Rooms.HandleRoomIssue;

/// <summary>Bước xử lý báo hỏng</summary>
public enum RoomIssueStep
{
    /// <summary>Nhận sửa (người / đơn vị + hẹn xong)</summary>
    Start,
    /// <summary>Sửa xong (đã làm gì + chi phí)</summary>
    Resolve,
    /// <summary>Mở lại</summary>
    Reopen,
}

/// <summary>Nhận sửa → sửa xong → (mở lại) một báo hư hỏng</summary>
public sealed record HandleRoomIssueCommand(Guid Id, RoomIssueStep Step, string? Assignee, DateOnly? DueDate, string? Resolution, decimal? Cost)
    : ICommand<RoomIssueDto>;

public sealed class HandleRoomIssueValidator : AbstractValidator<HandleRoomIssueCommand>
{
    public HandleRoomIssueValidator()
    {
        RuleFor(x => x.Assignee).NotEmpty().WithMessage("Nhập người / đơn vị nhận sửa").MaximumLength(200).When(x => x.Step == RoomIssueStep.Start);
        RuleFor(x => x.Resolution).NotEmpty().WithMessage("Ghi lại đã sửa gì").MaximumLength(2000).When(x => x.Step == RoomIssueStep.Resolve);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).Must(c => c is null || decimal.Truncate(c.Value) == c)
            .WithMessage("Chi phí VND không có số lẻ").When(x => x.Cost is not null);
    }
}

public sealed class HandleRoomIssueHandler(IAcademicDbContext db, RoomReader reader, AcademicAccess access, IClock clock, IAuditTrail audit)
    : ICommandHandler<HandleRoomIssueCommand, RoomIssueDto>
{
    public async Task<Result<RoomIssueDto>> Handle(HandleRoomIssueCommand cmd, CancellationToken ct)
    {
        var issue = await db.RoomIssues.FirstOrDefaultAsync(i => i.Id == new RoomIssueId(cmd.Id), ct);
        if (issue is null) return RoomErrors.IssueNotFound;
        var room = await db.Rooms.AsNoTracking().FirstAsync(r => r.Id == issue.RoomId, ct);
        if (!access.InBranch(AcademicAccess.Rooms, PermissionAction.Update, room.BranchId.Value)) return RoomErrors.IssueNotFound;

        var r = cmd.Step switch
        {
            RoomIssueStep.Start => issue.Start(cmd.Assignee!.Trim(), cmd.DueDate, clock.UtcNow),
            RoomIssueStep.Resolve => issue.Resolve(cmd.Resolution!.Trim(), cmd.Cost is { } c ? Money.FromTrusted(c) : null, clock.UtcNow),
            _ => issue.Reopen(),
        };
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info,
            cmd.Step switch { RoomIssueStep.Start => "Nhận sửa báo hỏng", RoomIssueStep.Resolve => "Sửa xong báo hỏng", _ => "Mở lại báo hỏng" },
            room.Name, issue.Title);
        return await reader.IssueAsync(issue.Id, ct);
    }
}
