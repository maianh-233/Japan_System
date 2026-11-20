using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Realtime;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.DecideCancelRequest;

/// <summary>
/// DT-16 bước 2 — giáo vụ duyệt / từ chối (lý do) đơn hủy buổi. Duyệt xong chọn tiếp: mời dạy thay (<c>POST substitute-invites</c>)
/// hoặc hủy buổi & xếp học bù (<c>POST class-sessions/{id}/cancel</c> rồi <c>POST class-sessions</c>).
/// Buổi / lớp không còn (E6) → đóng đơn. Đẩy realtime <c>cancel-request.decided</c> cho GV.
/// </summary>
public sealed record DecideCancelRequestCommand(Guid Id, bool Approve, string? Note) : ICommand<CancelRequestDto>;

public sealed class DecideCancelRequestValidator : AbstractValidator<DecideCancelRequestCommand>
{
    public DecideCancelRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Note).NotEmpty().When(x => !x.Approve).WithMessage("Cần ghi lý do từ chối");
    }
}

public sealed class DecideCancelRequestHandler(IAcademicDbContext db, RequestReader reader, AcademicAccess access, IClock clock,
    IRealtimeNotifier realtime, IAuditTrail audit, AcademicNotices notices) : ICommandHandler<DecideCancelRequestCommand, CancelRequestDto>
{
    public async Task<Result<CancelRequestDto>> Handle(DecideCancelRequestCommand cmd, CancellationToken ct)
    {
        var request = await db.SessionCancelRequests.FirstOrDefaultAsync(r => r.Id == new SessionCancelRequestId(cmd.Id), ct);
        if (request is null || !reader.InBranch(PermissionAction.Approve, request.BranchId)) return RequestErrors.CancelRequestNotFound;

        if (cmd.Approve)
        {
            var session = await db.ClassSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
            var classAlive = await db.Classes.AnyAsync(c => c.Id == request.ClassId, ct);
            if (request.IsPending && (session is null || !classAlive || session.Status == SessionStatus.Cancelled))
            {
                request.CloseOrphan(clock.UtcNow);
                await db.SaveChangesAsync(ct);
                return RequestErrors.SessionGone;
            }
        }

        var r = cmd.Approve ? request.Approve(access.ActorId, clock.UtcNow, cmd.Note?.Trim()) : request.Reject(access.ActorId, clock.UtcNow, cmd.Note ?? "");
        if (r.IsFailure) return r.Error;
        notices.CancelRequestDecided(request.TeacherId, await notices.SessionLabelAsync(request.ClassId, request.SessionId, ct), cmd.Approve, cmd.Note);
        await db.SaveChangesAsync(ct);

        var dto = (await reader.CancelDtosAsync([request], ct))[0];
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Approve ? "Duyệt đơn hủy buổi" : "Từ chối đơn hủy buổi",
            $"{dto.ClassCode} · buổi {dto.Session?.No}", dto.TeacherName);
        await realtime.ToEmployeesAsync([request.TeacherId], "cancel-request.decided", dto, ct);
        return dto;
    }
}
