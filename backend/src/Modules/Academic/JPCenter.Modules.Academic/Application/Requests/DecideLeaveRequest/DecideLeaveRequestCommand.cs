using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Realtime;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.DecideLeaveRequest;

/// <summary>
/// DT-18 bước 3 — giáo vụ duyệt / từ chối (lý do) đơn xin nghỉ. Duyệt: buổi gốc HV thành "vắng có phép" (DT-09.E7, đã có mặt thì giữ);
/// có đăng ký bù → HV có tên ở danh sách điểm danh buổi bù; buổi bù hết chỗ / không còn hợp lệ (E3) → vẫn duyệt nghỉ, bỏ đăng ký bù + cảnh báo.
/// Xin nghỉ nhiều buổi trong lớp (E6) → cảnh báo. Đẩy realtime <c>leave-request.decided</c> cho tài khoản đã gửi đơn.
/// </summary>
public sealed record DecideLeaveRequestCommand(Guid Id, bool Approve, string? Note) : ICommand<DecisionResult<LeaveRequestDto>>;

public sealed class DecideLeaveRequestValidator : AbstractValidator<DecideLeaveRequestCommand>
{
    public DecideLeaveRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Note).NotEmpty().When(x => !x.Approve).WithMessage("Cần ghi lý do từ chối");
    }
}

public sealed class DecideLeaveRequestHandler(IAcademicDbContext db, RequestReader reader, AcademicAccess access, ClassRoster roster, IClock clock,
    IRealtimeNotifier realtime, IAuditTrail audit) : ICommandHandler<DecideLeaveRequestCommand, DecisionResult<LeaveRequestDto>>
{
    /// <summary>⏳ DT-18.E6: ngưỡng "xin nghỉ quá N buổi / khóa" — chuyển sang Cài đặt chung khi có màn hình cấu hình</summary>
    public const int ManyLeavesFrom = 3;

    public async Task<Result<DecisionResult<LeaveRequestDto>>> Handle(DecideLeaveRequestCommand cmd, CancellationToken ct)
    {
        var request = await db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == new LeaveRequestId(cmd.Id), ct);
        if (request is null || !reader.InBranch(PermissionAction.Approve, request.BranchId)) return RequestErrors.LeaveRequestNotFound;
        if (!request.IsPending) return RequestErrors.NotPending;
        var warnings = new List<string>();

        if (!cmd.Approve)
        {
            if (request.Reject(access.ActorId, clock.UtcNow, cmd.Note ?? "") is { IsFailure: true } rejected) return rejected.Error;
        }
        else
        {
            var original = await db.ClassSessions.FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
            if (original is null || original.Status == SessionStatus.Cancelled)
            {
                request.Reject(access.ActorId, clock.UtcNow, RequestErrors.SessionGone.Message);
                await db.SaveChangesAsync(ct);
                return RequestErrors.SessionGone;
            }
            if (request.Approve(access.ActorId, clock.UtcNow, cmd.Note?.Trim()) is { IsFailure: true } approved) return approved.Error;
            original.MarkExcusedByLeave(request.StudentId, request.Id.Value);
            if (request.MakeupSessionId is { } mid && !await PlaceMakeupAsync(request, original, mid, ct))
            {
                request.DropMakeup();
                warnings.Add("Buổi học bù đã đủ chỗ hoặc không còn hợp lệ — đã duyệt nghỉ, bỏ đăng ký bù; báo HV chọn buổi khác");
            }

            var leaves = await db.LeaveRequests.CountAsync(l => l.StudentId == request.StudentId && l.ClassId == request.ClassId
                && l.Status == RequestStatus.Approved && l.Id != request.Id, ct) + 1;
            if (leaves >= ManyLeavesFrom) warnings.Add($"HV đã xin nghỉ {leaves} buổi trong lớp này");
        }
        await db.SaveChangesAsync(ct);

        var dto = (await reader.LeaveDtosAsync([request], ct))[0];
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Approve ? "Duyệt đơn xin nghỉ" : "Từ chối đơn xin nghỉ",
            $"{dto.ClassCode} · buổi {dto.Session?.No}", dto.StudentName);
        await realtime.ToAccountsAsync([request.ByAccountId], "leave-request.decided", dto, ct);
        return new DecisionResult<LeaveRequestDto>(dto, warnings);
    }

    /// <summary>Thêm HV vào danh sách buổi bù nếu buổi còn hợp lệ và còn chỗ (E3)</summary>
    private async Task<bool> PlaceMakeupAsync(LeaveRequest request, ClassSession original, ClassSessionId makeupId, CancellationToken ct)
    {
        var makeup = await db.ClassSessions.FirstOrDefaultAsync(s => s.Id == makeupId, ct);
        if (makeup is null || makeup.Status != SessionStatus.Scheduled || makeup.IsConfirmed || makeup.StartsAt <= clock.LocalNow) return false;
        var target = await db.Classes.IgnoreQueryFilters().FirstAsync(c => c.Id == makeup.ClassId, ct);
        if (!MakeupRules.HasSeat(target, await roster.EnrolledAsync(target.Id.Value, ct) ?? 0, makeup)) return false;
        makeup.AddMakeupGuest(request.StudentId, original.Id, request.Id.Value);
        return true;
    }
}
