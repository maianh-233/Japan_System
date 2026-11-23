using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Paging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.HrRequests.Common;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.HrRequests.GetHrRequests;

/// <summary>Tab danh sách đơn (DN-04)</summary>
public enum HrRequestTab
{
    /// <summary>Chờ tôi duyệt — không cần mã quyền</summary>
    Todo,
    /// <summary>Đơn của tôi — không cần mã quyền</summary>
    Mine,
    /// <summary>Tất cả trong phạm vi <c>hr.requests.view</c> (theo chi nhánh người gửi; own = mình + cấp dưới)</summary>
    All,
}

/// <summary>DN-04 — danh sách đơn theo tab, lọc loại / trạng thái / chi nhánh, mới nhất trước; kèm thống kê.</summary>
public sealed record GetHrRequestsQuery(HrRequestTab Tab, HrRequestType? Type, HrRequestStatus? Status, Guid? BranchId, PageRequest Paging)
    : IQuery<HrRequestListDto>;

public sealed class GetHrRequestsHandler(IHrDbContext db, HrRequestReader reader, HrScope scope, CurrentEmployee current, IClock clock)
    : IQueryHandler<GetHrRequestsQuery, HrRequestListDto>
{
    public static readonly Error ViewForbidden = Error.Forbidden("hr.request.view_forbidden", "Bạn không có quyền xem tất cả đơn từ");

    public async Task<Result<HrRequestListDto>> Handle(GetHrRequestsQuery q, CancellationToken ct)
    {
        if (q.Tab == HrRequestTab.All && !scope.Can(HrScope.Requests, PermissionAction.View)) return ViewForbidden;

        var all = await db.HrRequests.AsNoTracking().ToListAsync(ct);
        var employees = await db.Employees.AsNoTracking().ToListAsync(ct);
        var me = current.Id;
        var visibleEmployees = scope.Employees(employees, HrScope.Requests, PermissionAction.View).Select(e => e.Id).ToHashSet();
        var perm = scope.Can(HrScope.Requests, PermissionAction.View);
        bool InAll(HrRequest r) => perm && visibleEmployees.Contains(r.EmployeeId);
        bool Mine(HrRequest r) => me is { } id && r.EmployeeId.Value == id;

        var tabRows = q.Tab switch
        {
            HrRequestTab.Todo => all.Where(reader.IsTodo),
            HrRequestTab.Mine => all.Where(Mine),
            _ => all.Where(InAll),
        };
        var filtered = tabRows
            .Where(r => q.Type is not { } t || r.Type == t)
            .Where(r => q.Status is not { } s || r.Status == s)
            .Where(r => q.BranchId is not { } b || r.BranchId.Value == b)
            .OrderByDescending(r => r.RequestedAt)
            .ToList();

        var names = employees.ToDictionary(e => e.Id.Value, e => e.FullName);
        var page = q.Paging.SafePage;
        var size = q.Paging.SafePageSize;
        var items = filtered.Skip((page - 1) * size).Take(size).Select(r => reader.ToDto(r, names)).ToList();

        // Thống kê trên phạm vi "Tất cả" nếu có quyền xem, không thì trên đơn của tôi
        var statBase = (perm ? all.Where(InAll) : all.Where(Mine)).ToList();
        var today = clock.Today;
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(6);
        var stats = new HrRequestStatsDto(
            all.Count(reader.IsTodo),
            statBase.Count(r => r.Type == HrRequestType.Leave && r.Status == HrRequestStatus.Approved && r.From <= weekEnd && r.To >= weekStart),
            statBase.Where(r => r.Status == HrRequestStatus.Pending && r.Type is HrRequestType.Proposal or HrRequestType.Advance)
                .Sum(r => r.Amount?.Amount ?? 0));
        return new HrRequestListDto(items, filtered.Count, page, size, stats);
    }
}
