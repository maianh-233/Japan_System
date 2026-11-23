using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.HrRequests.Common;
using JPCenter.Modules.Hr.Contracts.IntegrationEvents;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Work.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.HrRequests.DecideHrRequest;

/// <summary>DN-02 — duyệt (<paramref name="Approve"/> = true, ghi chú tùy chọn) hoặc từ chối (lý do bắt buộc) bước hiện tại.</summary>
public sealed record DecideHrRequestCommand(Guid Id, bool Approve, string? Comment) : ICommand<HrRequestDto>;

public sealed class DecideHrRequestValidator : AbstractValidator<DecideHrRequestCommand>
{
    public DecideHrRequestValidator() => RuleFor(x => x.Comment).MaximumLength(1000);
}

/// <summary>
/// Chưa tới lượt (E1) · đã xử lý (E2) · tự duyệt (E3) · từ chối không lý do (E4) · duyệt thay ghi người thật (E7).
/// Bước cuối duyệt → áp dụng: nghỉ có lương trừ phép năm (không đủ → E5), đổi ca kiểm 2 ca còn nguyên (E6) rồi phát
/// <see cref="ShiftSwapApproved"/> cho Work; thông báo người gửi. Còn bước sau → thông báo người duyệt kế tiếp.
/// </summary>
public sealed class DecideHrRequestHandler(IHrDbContext db, HrRequestReader reader, CurrentEmployee current, HrNotices notices,
    IIntegrationEventPublisher events, IShiftSchedule shifts, IAuditTrail audit, IClock clock) : ICommandHandler<DecideHrRequestCommand, HrRequestDto>
{
    public async Task<Result<HrRequestDto>> Handle(DecideHrRequestCommand cmd, CancellationToken ct)
    {
        var request = await reader.FindForUpdateAsync(cmd.Id, ct);
        if (request is null) return HrRequestErrors.NotFound;
        if (current.Id is not { } me) return HrRequestErrors.NotYourTurn;
        if (request.Status != HrRequestStatus.Pending) return HrRequestErrors.NotPending;
        if (request.EmployeeId.Value == me) return HrRequestErrors.SelfApprove;
        var canOverride = reader.CanOverride(request);
        if (!request.CanDecide(me, canOverride)) return HrRequestErrors.NotYourTurn;

        var lastStep = request.Steps.Count(s => s.Decision is null) == 1;
        var requester = await db.Employees.FirstAsync(e => e.Id == request.EmployeeId, ct);
        if (cmd.Approve && lastStep)
        {
            if (request.DeductsLeave && requester.UseLeave(request.Days!.Value) is { IsFailure: true }) return HrRequestErrors.LeaveExceeded;
            if (request.Type == HrRequestType.ShiftSwap && !await SwapStillValidAsync(request, ct)) return HrRequestErrors.SwapStale;
        }

        var decided = request.Decide(me, current.Name, canOverride, cmd.Approve ? ApprovalDecision.Approved : ApprovalDecision.Rejected, cmd.Comment,
            clock.UtcNow);
        if (decided.IsFailure) return decided.Error;

        var what = HrRequestReader.Describe(request);
        switch (request.Status)
        {
            case HrRequestStatus.Approved:
                if (request.Type == HrRequestType.ShiftSwap)
                    events.Publish(new ShiftSwapApproved(request.Id.Value, request.Code, request.EmployeeId.Value, request.SwapMyShiftId!.Value,
                        request.SwapWithEmployeeId!.Value.Value, request.SwapTheirShiftId!.Value));
                notices.ToEmployees([request.EmployeeId.Value], $"Đơn {request.Code} đã được duyệt", what, "success", HrNotices.MyAttendancePage,
                    "hr.request.approved");
                break;
            case HrRequestStatus.Rejected:
                notices.ToEmployees([request.EmployeeId.Value], $"Đơn {request.Code} bị từ chối", $"{what} — {cmd.Comment?.Trim()}", "danger",
                    HrNotices.MyAttendancePage, "hr.request.rejected");
                break;
            default:
                var next = request.CurrentStep!;
                if (next.ApproverId is { } nextId)
                    notices.ToEmployees([nextId], $"Đơn {request.Code} chờ bạn duyệt", $"{requester.FullName}: {what}", "info", HrNotices.RequestsPage,
                        "hr.request.next_step");
                else
                    notices.ToPermission($"{HrScope.Requests}.approve", request.BranchId.Value, $"Đơn {request.Code} chờ duyệt",
                        $"{requester.FullName}: {what} — bước \"{next.Role}\"", "info", HrNotices.RequestsPage, "hr.request.next_step");
                break;
        }

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Approve ? "Duyệt đơn" : "Từ chối đơn", request.Code,
            $"{requester.FullName}: {what}", branchId: request.BranchId.Value);
        return await reader.DetailAsync(request, ct);
    }

    /// <summary>DN-02.E6 — 2 ca vẫn còn, vẫn đúng người, chưa qua</summary>
    private async Task<bool> SwapStillValidAsync(HrRequest r, CancellationToken ct)
    {
        var found = await shifts.FindManyAsync([r.SwapMyShiftId!.Value, r.SwapTheirShiftId!.Value], ct);
        var today = clock.Today;
        return found.TryGetValue(r.SwapMyShiftId.Value, out var mine) && found.TryGetValue(r.SwapTheirShiftId.Value, out var theirs)
            && mine.EmployeeId == r.EmployeeId.Value && theirs.EmployeeId == r.SwapWithEmployeeId!.Value.Value
            && mine.Date >= today && theirs.Date >= today;
    }
}
