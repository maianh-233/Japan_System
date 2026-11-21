using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Common;

/// <summary>
/// Đội Sale cho chia lead / SLA (CRM-09 → CRM-11): ai còn làm (Hr), ai đang trong ca, mỗi người đang giữ bao nhiêu lead mở.
/// "Trong ca" = giờ hành chính của Cài đặt chung (thứ 2 → thứ 7, CN nghỉ). ⏳ Sale làm theo ca theo Xếp ca khi có module Work (giai đoạn 21).
/// </summary>
public sealed class SalesTeam(IEmployeeDirectory employees, ICenterSettings settings, ICrmDbContext db, IClock clock)
{
    public const string SalesDepartment = "sales";

    /// <summary>Nhân viên phòng Sale còn làm việc (không nghỉ việc, không nghỉ dài ngày)</summary>
    public static bool IsWorkingSale(EmployeeSummary e) => e.Department == SalesDepartment && !e.HasQuit && e.Status != "leave";

    public async Task<RoutingContext> ContextAsync(CancellationToken ct)
    {
        var people = (await employees.ListActiveAsync(ct)).ToDictionary(e => e.Id);
        var office = (await settings.GetAsync(ct)).Office;
        var now = clock.LocalNow;
        var time = TimeOnly.FromDateTime(now);
        var end = now.DayOfWeek == DayOfWeek.Saturday ? office.SatTo : office.To;
        var officeOpen = now.DayOfWeek != DayOfWeek.Sunday && end is { } e && time >= office.From && time < e;

        var load = await OpenLoadAsync(ct);
        return new RoutingContext(
            id => people.TryGetValue(id, out var p) && IsWorkingSale(p),
            _ => officeOpen,
            id => load.GetValueOrDefault(id),
            id => people.TryGetValue(id, out var p) ? p.FullName : "Sale");
    }

    /// <summary>Số lead mở (mới / đang tư vấn / hẹn test, không ẩn) theo Sale</summary>
    public async Task<Dictionary<Guid, int>> OpenLoadAsync(CancellationToken ct) =>
        await db.Leads.AsNoTracking()
            .Where(l => l.AssigneeId != null && !l.Hidden && Lead.OpenStatuses.Contains(l.Status))
            .GroupBy(l => l.AssigneeId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    /// <summary>CRM-02.E7 — Sale chọn tay phải còn làm và thuộc chi nhánh của lead</summary>
    public async Task<bool> CanTakeAsync(Guid saleId, Guid branchId, CancellationToken ct) =>
        await employees.FindAsync(saleId, ct) is { } e && !e.HasQuit && e.BranchId == branchId;
}
