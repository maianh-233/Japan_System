using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.HrRequests.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Work.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.HrRequests.CreateHrRequest;

/// <summary>
/// DN-01 — gửi đơn. <paramref name="EmployeeId"/> null = đơn của tôi (trang cá nhân); có giá trị = nhân sự tạo hộ (<c>hr.requests.create</c>, trong phạm vi).
/// Đề xuất / tạm ứng không cần ngày (mặc định hôm nay); đổi ca lấy ngày theo 2 ca.
/// </summary>
public sealed record CreateHrRequestCommand(Guid? EmployeeId, HrRequestType Type, DateOnly? From, DateOnly? To, bool? PaidLeave, decimal? Amount,
    string? Title, Guid? SwapMyShiftId, Guid? SwapWithEmployeeId, Guid? SwapTheirShiftId, string Reason) : ICommand<HrRequestDto>;

public sealed class CreateHrRequestValidator : AbstractValidator<CreateHrRequestCommand>
{
    public CreateHrRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithErrorCode("hr.request.reason_missing").WithMessage("Nhập lý do").MaximumLength(1000);
        RuleFor(x => x.Title).MaximumLength(200);
    }
}

/// <summary>
/// Kiểm theo loại (E1 → E8), dựng cấp duyệt (<see cref="ApprovalChain"/>): quản lý trực tiếp + Phó GĐ / Kế toán trưởng / Giám đốc tìm theo vị trí,
/// sinh mã <c>DT-{năm}{4 số}</c>, thông báo người duyệt bước 1.
/// </summary>
public sealed class CreateHrRequestHandler(IHrDbContext db, CurrentEmployee current, HrScope scope, HrRequestReader reader, HrNotices notices,
    IShiftSchedule shifts, IClock clock) : ICommandHandler<CreateHrRequestCommand, HrRequestDto>
{
    public static readonly Error EmployeeNotFound = Error.NotFound("hr.request.employee_not_found", "Không tìm thấy nhân viên trong phạm vi của bạn");

    public async Task<Result<HrRequestDto>> Handle(CreateHrRequestCommand cmd, CancellationToken ct)
    {
        var all = await db.Employees.AsNoTracking().ToListAsync(ct);
        Employee? requester;
        if (cmd.EmployeeId is { } forId)
            requester = scope.Employees(all, HrScope.Requests, PermissionAction.Create).FirstOrDefault(e => e.Id.Value == forId);
        else
            requester = current.Id is { } me ? all.FirstOrDefault(e => e.Id.Value == me) : null;
        if (requester is null) return cmd.EmployeeId is null ? HrRequestErrors.NotStaff : EmployeeNotFound;
        if (requester.IsTeaching || requester.HasQuit) return HrRequestErrors.NotStaff;

        var today = clock.Today;
        DateOnly from, to;
        if (cmd.Type == HrRequestType.ShiftSwap)
        {
            var swap = await CheckSwapAsync(requester, cmd, all, today, ct);
            if (swap.IsFailure) return swap.Error;
            (from, to) = swap.Value;
        }
        else if (cmd.Type is HrRequestType.Proposal or HrRequestType.Advance)
            (from, to) = (cmd.From ?? today, cmd.To ?? cmd.From ?? today);
        else
        {
            if (cmd.From is not { } f) return HrRequestErrors.InvalidDates;
            (from, to) = (f, cmd.Type == HrRequestType.LateEarly ? cmd.To ?? f : cmd.To ?? f);
        }

        if (cmd.Type is HrRequestType.Leave or HrRequestType.BusinessTrip)
        {
            var blocking = new[] { HrRequestType.Leave, HrRequestType.BusinessTrip };
            var overlap = await db.HrRequests.AnyAsync(r => r.EmployeeId == requester.Id && blocking.Contains(r.Type)
                && (r.Status == HrRequestStatus.Pending || r.Status == HrRequestStatus.Approved) && r.From <= to && r.To >= from, ct);
            if (overlap) return HrRequestErrors.Overlap;
        }
        var paid = cmd.PaidLeave ?? true;
        if (cmd.Type == HrRequestType.Leave && to >= from && paid && HrRequest.WorkDays(from, to) > requester.LeaveBalance)
            return HrRequestErrors.LeaveExceeded;

        Money? amount = null;
        if (cmd.Amount is { } a)
        {
            if (a < 0 || decimal.Truncate(a) != a) return HrRequestErrors.AmountRequired;
            amount = Money.FromTrusted(a);
        }

        var days = cmd.Type == HrRequestType.Leave && to >= from ? HrRequest.WorkDays(from, to) : 0;
        var steps = ApprovalChain.Build(cmd.Type, requester.Id.Value, Manager(requester, all), Holders(all), days, amount);
        var details = new HrRequestDetails(paid, amount, cmd.Title, cmd.SwapMyShiftId,
            cmd.SwapWithEmployeeId is { } w ? new EmployeeId(w) : null, cmd.SwapTheirShiftId);
        var created = HrRequest.Create(await NextCodeAsync(today.Year, ct), cmd.Type, requester.Id, requester.BranchId, from, to, cmd.Reason, details,
            steps, clock.UtcNow, today);
        if (created.IsFailure) return created.Error;
        var request = created.Value;

        db.HrRequests.Add(request);
        NotifyFirstApprover(request, requester.FullName);
        await db.SaveChangesAsync(ct);
        return await reader.DetailAsync(request, ct);
    }

    /// <summary>DN-01.E6 — ca của tôi + ca của đồng nghiệp: đúng chủ, từ hôm nay, cùng chi nhánh, cả 2 làm theo ca, chưa nằm trong đơn đổi khác đang chờ</summary>
    private async Task<Result<(DateOnly From, DateOnly To)>> CheckSwapAsync(Employee requester, CreateHrRequestCommand cmd, List<Employee> all,
        DateOnly today, CancellationToken ct)
    {
        if (cmd is not { SwapMyShiftId: { } mineId, SwapWithEmployeeId: { } withId, SwapTheirShiftId: { } theirsId }
            || withId == requester.Id.Value || !requester.ShiftBased) return HrRequestErrors.InvalidSwap;
        var colleague = all.FirstOrDefault(e => e.Id.Value == withId);
        if (colleague is null || colleague.HasQuit || !colleague.ShiftBased || colleague.BranchId != requester.BranchId) return HrRequestErrors.InvalidSwap;

        var found = await shifts.FindManyAsync([mineId, theirsId], ct);
        if (!found.TryGetValue(mineId, out var mine) || !found.TryGetValue(theirsId, out var theirs)) return HrRequestErrors.InvalidSwap;
        if (mine.EmployeeId != requester.Id.Value || theirs.EmployeeId != withId || mine.Date < today || theirs.Date < today
            || mine.BranchId != theirs.BranchId) return HrRequestErrors.InvalidSwap;

        var pending = await db.HrRequests.AnyAsync(r => r.Type == HrRequestType.ShiftSwap && r.Status == HrRequestStatus.Pending
            && (r.SwapMyShiftId == mineId || r.SwapTheirShiftId == mineId || r.SwapMyShiftId == theirsId || r.SwapTheirShiftId == theirsId), ct);
        if (pending) return HrRequestErrors.InvalidSwap;
        return (mine.Date < theirs.Date ? mine.Date : theirs.Date, mine.Date > theirs.Date ? mine.Date : theirs.Date);
    }

    private static ApproverRef? Manager(Employee requester, List<Employee> all) =>
        requester.ManagerId is { } m && all.FirstOrDefault(e => e.Id == m) is { HasQuit: false } manager
            ? new ApproverRef(manager.Id.Value, manager.FullName) : null;

    /// <summary>Người giữ chức vụ: nhân viên chưa nghỉ, vị trí bắt đầu bằng tên chức vụ (người vào làm sớm nhất nếu có nhiều)</summary>
    public static ApproverHolders Holders(IEnumerable<Employee> all)
    {
        var active = all.Where(e => !e.HasQuit).OrderBy(e => e.JoinedAt).ToList();
        ApproverRef? Find(string prefix) => active.FirstOrDefault(e => e.Position.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) is { } e
            ? new ApproverRef(e.Id.Value, e.FullName) : null;
        return new ApproverHolders(Find("Phó giám đốc"), Find("Kế toán trưởng"), Find("Giám đốc"));
    }

    private async Task<string> NextCodeAsync(int year, CancellationToken ct)
    {
        var prefix = $"DT-{year}";
        var codes = await db.HrRequests.AsNoTracking().Where(r => r.Code.StartsWith(prefix)).Select(r => r.Code).ToListAsync(ct);
        var max = codes.Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:0000}";
    }

    private void NotifyFirstApprover(HrRequest r, string requesterName)
    {
        var title = $"Đơn {r.Code} chờ bạn duyệt";
        var desc = $"{requesterName}: {HrRequestReader.Describe(r)}";
        if (r.CurrentStep?.ApproverId is { } approver)
            notices.ToEmployees([approver], title, desc, "info", HrNotices.RequestsPage, "hr.request.created");
        else
            notices.ToPermission($"{HrScope.Requests}.approve", r.BranchId.Value, $"Đơn {r.Code} chờ duyệt", desc, "info", HrNotices.RequestsPage,
                "hr.request.created");
    }
}
