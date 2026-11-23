using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.HrRequests.Common;

/// <summary>Đọc đơn ra DTO + tính quyền của người đang xem (được duyệt bước hiện tại / được hủy).</summary>
public sealed class HrRequestReader(IHrDbContext db, HrScope scope, CurrentEmployee current)
{
    public const string OpenApprover = "Người có quyền duyệt đơn";

    /// <summary>Người đang xem duyệt thay / duyệt bước mở được ở chi nhánh của đơn</summary>
    public bool CanOverride(HrRequest r) => scope.InBranch(HrScope.Requests, PermissionAction.Approve, r.BranchId.Value);

    public bool CanDecide(HrRequest r) => current.Id is { } me && r.CanDecide(me, CanOverride(r));

    /// <summary>Đơn nằm trong tab "Chờ tôi duyệt": đến lượt mình, hoặc bước mở mà mình có quyền duyệt (không gồm đơn của chính mình)</summary>
    public bool IsTodo(HrRequest r) =>
        current.Id is { } me && r.CurrentStep is { } step && r.EmployeeId.Value != me
        && (step.ApproverId == me || (step.ApproverId is null && CanOverride(r)));

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(CancellationToken ct) =>
        await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id.Value, e => e.FullName, ct);

    public HrRequestDto ToDto(HrRequest r, IReadOnlyDictionary<Guid, string> names)
    {
        var steps = r.Steps.OrderBy(s => s.Order).Select(s => new ApprovalStepDto(s.Order, s.Role,
            s.Decision is not null ? (s.DecidedByName ?? "?") + (s.OnBehalf ? " (duyệt thay)" : "") : s.ApproverName ?? OpenApprover,
            s.ApproverId, s.Decision, s.At, s.Comment, s.OnBehalf, s.DecidedById)).ToList();
        var swap = r is { SwapMyShiftId: { } mine, SwapWithEmployeeId: { } with, SwapTheirShiftId: { } theirs }
            ? new SwapDto(mine, with.Value, theirs, names.GetValueOrDefault(with.Value))
            : null;
        return new HrRequestDto(r.Id.Value, r.Code, r.Type, r.EmployeeId.Value, names.GetValueOrDefault(r.EmployeeId.Value, "?"), r.BranchId.Value,
            r.From, r.To, r.Days, r.PaidLeave, swap, r.Amount?.Amount, r.Title, r.Reason, r.Status, steps, r.RequestedAt, r.CurrentStep?.Order,
            CanDecide(r), r.Status == HrRequestStatus.Pending && current.Id == r.EmployeeId.Value);
    }

    public async Task<HrRequestDto> DetailAsync(HrRequest r, CancellationToken ct) => ToDto(r, await NamesAsync(ct));

    /// <summary>Đơn theo Id (đang theo dõi để sửa)</summary>
    public Task<HrRequest?> FindForUpdateAsync(Guid id, CancellationToken ct) =>
        db.HrRequests.FirstOrDefaultAsync(r => r.Id == new HrRequestId(id), ct);

    /// <summary>Tổng tiền (đề xuất / tạm ứng) dạng hiển thị trong thông báo</summary>
    public static string Describe(HrRequest r) => r.Type switch
    {
        HrRequestType.Leave => $"nghỉ phép {r.From:dd/MM}–{r.To:dd/MM} ({r.Days:0.#} ngày{(r.PaidLeave == true ? "" : ", không lương")})",
        HrRequestType.BusinessTrip => $"công tác {r.From:dd/MM}–{r.To:dd/MM}",
        HrRequestType.LateEarly => $"đi muộn / về sớm ngày {r.From:dd/MM}",
        HrRequestType.ShiftSwap => $"đổi ca ngày {r.From:dd/MM}" + (r.To != r.From ? $"–{r.To:dd/MM}" : ""),
        HrRequestType.Proposal => $"đề xuất \"{r.Title}\"" + (r.Amount is { } a ? $" {Vnd(a)}" : ""),
        _ => $"tạm ứng {Vnd(r.Amount ?? Money.Zero)}",
    };

    private static string Vnd(Money m) => m.Amount.ToString("#,0", global::System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + "đ";
}
