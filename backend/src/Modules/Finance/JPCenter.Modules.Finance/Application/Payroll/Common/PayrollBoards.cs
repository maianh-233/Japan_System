using System.Text.Json;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Payroll.Common;

/// <summary>
/// Dựng bảng lương tháng theo phạm vi chi nhánh: chi nhánh ĐANG KHÓA lấy dòng từ bản chốt (số liệu không đổi sau khi chốt),
/// còn lại tạm tính (<see cref="PayrollCalculator"/>). Kèm khóa, hạn chốt (<c>payroll.lockDay</c>), cảnh báo quá hạn.
/// </summary>
public sealed class PayrollBoards(IFinanceDbContext db, PayrollCalculator calculator, FinanceAccess access, IBranchDirectory branchDirectory,
    ICenterSettings settings, PeopleNames names, IClock clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string PageOf(PayrollKind kind) => kind == PayrollKind.Teacher ? FinanceAccess.TeacherPayroll : FinanceAccess.StaffPayroll;

    public static string KindText(PayrollKind kind) => kind == PayrollKind.Teacher ? "teacher" : "staff";

    /// <summary>
    /// Chi nhánh xét: <paramref name="branchIds"/> (rỗng = mọi chi nhánh đang hoạt động) giao với phạm vi quyền <paramref name="action"/>.
    /// Chọn chi nhánh ngoài phạm vi → <see cref="FinanceAccess.BranchNotAllowed"/>.
    /// </summary>
    public async Task<Result<List<Guid>>> BranchesAsync(PayrollKind kind, PermissionAction action, IReadOnlyCollection<Guid>? branchIds, CancellationToken ct)
    {
        var allowed = access.Branches(PageOf(kind), action);
        if (branchIds is { Count: > 0 })
        {
            if (allowed is not null && branchIds.Any(b => !allowed.Contains(b))) return FinanceAccess.BranchNotAllowed;
            return branchIds.Distinct().ToList();
        }
        var all = (await branchDirectory.ListAsync(true, ct)).Select(b => b.Id);
        return all.Where(b => allowed is null || allowed.Contains(b)).ToList();
    }

    public async Task<PayrollBoardDto<TeacherPayDto>> TeacherAsync(string month, IReadOnlyList<Guid> branches, CancellationToken ct)
    {
        var locks = await ActiveLocksAsync(PayrollKind.Teacher, month, branches, ct);
        var live = branches.Where(b => Snapshot<TeacherPayDto>(locks.GetValueOrDefault(b)) is null).ToList();
        var rows = live.Count == 0 ? [] : await calculator.TeachersAsync(month, live, null, ct);
        rows.AddRange(branches.SelectMany(b => Snapshot<TeacherPayDto>(locks.GetValueOrDefault(b)) ?? []));
        rows = rows.OrderBy(r => r.FullName).ThenBy(r => r.BranchId).ToList();
        return await BoardAsync(PayrollKind.Teacher, month, branches, rows, locks, rows.Sum(r => r.PendingCount), rows.Sum(r => r.Total), ct);
    }

    public async Task<PayrollBoardDto<StaffPayDto>> StaffAsync(string month, IReadOnlyList<Guid> branches, CancellationToken ct)
    {
        var locks = await ActiveLocksAsync(PayrollKind.Staff, month, branches, ct);
        var live = branches.Where(b => Snapshot<StaffPayDto>(locks.GetValueOrDefault(b)) is null).ToList();
        var rows = live.Count == 0 ? [] : await calculator.StaffAsync(month, live, null, ct);
        rows.AddRange(branches.SelectMany(b => Snapshot<StaffPayDto>(locks.GetValueOrDefault(b)) ?? []));
        rows = rows.OrderBy(r => r.FullName).ToList();
        return await BoardAsync(PayrollKind.Staff, month, branches, rows, locks, 0, rows.Sum(r => r.Net), ct);
    }

    /// <summary>Dòng của bản chốt; bản chốt rỗng (dữ liệu mẫu chưa có chi tiết) → null = tạm tính</summary>
    public static List<T>? Snapshot<T>(PayrollLock? l)
    {
        if (l is null || !l.IsLocked || string.IsNullOrWhiteSpace(l.Lines)) return null;
        try
        {
            var rows = JsonSerializer.Deserialize<List<T>>(l.Lines, Json);
            return rows is { Count: > 0 } ? rows : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<Dictionary<Guid, PayrollLock>> ActiveLocksAsync(PayrollKind kind, string month, IReadOnlyCollection<Guid> branches, CancellationToken ct)
    {
        var ids = branches.Select(b => new BranchId(b)).ToList();
        var list = await db.PayrollLocks.AsNoTracking().Where(l => l.Kind == kind && l.Month == month && ids.Contains(l.BranchId)).ToListAsync(ct);
        return list.Where(l => l.IsLocked).ToDictionary(l => l.BranchId.Value);
    }

    public async Task<IReadOnlyList<PayrollLockDto>> LockDtosAsync(IEnumerable<PayrollLock> locks, CancellationToken ct)
    {
        var list = locks.ToList();
        var by = await names.OfAsync(list.Select(l => (Guid?)l.LockedBy), ct);
        return list.Select(l => new PayrollLockDto(KindText(l.Kind), l.Month, l.BranchId.Value, by.GetValueOrDefault(l.LockedBy, "—"), l.LockedAt,
            l.Total.Amount, l.PaidAt)).ToList();
    }

    private async Task<PayrollBoardDto<T>> BoardAsync<T>(PayrollKind kind, string month, IReadOnlyList<Guid> branches, IReadOnlyList<T> rows,
        Dictionary<Guid, PayrollLock> locks, int pending, decimal total, CancellationToken ct)
    {
        var lockDay = (await settings.GetAsync(ct)).PayrollLockDay;
        var allLocked = branches.Count > 0 && branches.All(locks.ContainsKey);
        return new PayrollBoardDto<T>(month, branches, rows, await LockDtosAsync(locks.Values, ct), allLocked,
            PayrollMonth.LockDeadline(month, lockDay), PayrollMonth.IsLockOverdue(month, lockDay, clock.Today, allLocked), pending, total);
    }
}
