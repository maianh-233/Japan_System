using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IHrLeaveCalendar"/> cho Work (lưới xếp ca).</summary>
internal sealed class HrLeaveCalendar(HrDbContext db) : IHrLeaveCalendar
{
    public async Task<IReadOnlyList<ApprovedLeave>> ApprovedLeavesAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to,
        CancellationToken ct)
    {
        var ids = employeeIds.Distinct().Select(x => new EmployeeId(x)).ToList();
        var rows = await db.HrRequests.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.Status == HrRequestStatus.Approved && r.From <= to && r.To >= from
                && (r.Type == HrRequestType.Leave || r.Type == HrRequestType.BusinessTrip))
            .ToListAsync(ct);
        return rows.Select(r => new ApprovedLeave(r.EmployeeId.Value, r.From, r.To, SnakeCaseEnumConverter<HrRequestType>.ToText(r.Type))).ToList();
    }

    public async Task<IReadOnlySet<Guid>> PendingSwapShiftIdsAsync(IReadOnlyCollection<Guid> shiftIds, CancellationToken ct)
    {
        if (shiftIds.Count == 0) return new HashSet<Guid>();
        var ids = shiftIds.Distinct().ToList();
        var rows = await db.HrRequests.AsNoTracking()
            .Where(r => r.Type == HrRequestType.ShiftSwap && r.Status == HrRequestStatus.Pending
                && ((r.SwapMyShiftId != null && ids.Contains(r.SwapMyShiftId.Value)) || (r.SwapTheirShiftId != null && ids.Contains(r.SwapTheirShiftId.Value))))
            .Select(r => new { r.SwapMyShiftId, r.SwapTheirShiftId })
            .ToListAsync(ct);
        return rows.SelectMany(r => new[] { r.SwapMyShiftId, r.SwapTheirShiftId }).OfType<Guid>().Where(ids.Contains).ToHashSet();
    }
}
