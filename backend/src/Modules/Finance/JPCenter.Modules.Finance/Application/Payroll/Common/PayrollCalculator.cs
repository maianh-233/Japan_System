using JPCenter.Application.Common.Time;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Commissions.Common;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Payroll.Common;

/// <summary>
/// Tính lương tạm tính (chưa xét bản chốt) từ dữ liệu nguồn: buổi dạy (Academic), hồ sơ lương / công tháng (Hr), điều chỉnh, hoa hồng (TC-12).
/// Công thức ở Domain (<see cref="TeacherPayCalculator"/>, <see cref="StaffPayCalculator"/>), tham số <see cref="PayrollRules.Default"/>.
/// </summary>
public sealed class PayrollCalculator(IFinanceDbContext db, IPayrollSource hr, ITeachingLog teaching, CommissionCalculator commissions, PeopleNames names)
{
    public static PayrollRules Rules => PayrollRules.Default;

    /// <summary>
    /// TC-13 — phiếu lương GV / trợ giảng theo từng chi nhánh trong <paramref name="branches"/>: buổi tính vào chi nhánh của lớp (E5), lương cứng
    /// và điều chỉnh ở chi nhánh chính của GV. <paramref name="branches"/> null = gộp mọi chi nhánh thành 1 phiếu / người (cổng GV).
    /// </summary>
    public async Task<List<TeacherPayDto>> TeachersAsync(string month, IReadOnlyCollection<Guid>? branches, Guid? teacherId, CancellationToken ct)
    {
        var (first, last) = PayrollMonth.Range(month);
        var profiles = (await hr.ProfilesAsync(first, last, ct)).Where(p => p.IsTeacher && (teacherId is null || p.Id == teacherId)).ToList();
        if (profiles.Count == 0) return [];
        var sessions = (await teaching.SessionsAsync(first, last, teacherId, ct)).ToLookup(s => s.TeacherId);
        var adjusts = await AdjustsAsync(month, profiles.Select(p => p.Id).ToList(), ct);

        var rows = new List<TeacherPayDto>();
        foreach (var p in profiles)
        {
            var mine = sessions[p.Id].ToList();
            foreach (var branch in branches?.Select(b => (Guid?)b) ?? [null])
            {
                var isMain = branch is null || branch == p.BranchId;
                var inBranch = mine.Where(s => branch is null || s.BranchId == branch).ToList();
                if (!isMain && inBranch.Count == 0) continue;
                rows.Add(Teacher(p, branch ?? p.BranchId, inBranch, isMain ? adjusts[p.Id].ToList() : [], isMain));
            }
        }
        return rows;
    }

    /// <summary>TC-14 — phiếu lương NV (<c>kind = staff</c>) của các chi nhánh (chi nhánh chính của NV); null = mọi chi nhánh</summary>
    public async Task<List<StaffPayDto>> StaffAsync(string month, IReadOnlyCollection<Guid>? branches, Guid? employeeId, CancellationToken ct)
    {
        var (first, last) = PayrollMonth.Range(month);
        var profiles = (await hr.ProfilesAsync(first, last, ct))
            .Where(p => p.Kind == "staff" && (branches is null || branches.Contains(p.BranchId)) && (employeeId is null || p.Id == employeeId)).ToList();
        if (profiles.Count == 0) return [];
        var ids = profiles.Select(p => p.Id).ToList();
        var days = await hr.WorkDaysAsync(ids, first, last, ct);
        var commission = profiles.Any(p => p.Department == CommissionCalculator.SalesDepartment)
            ? (await commissions.ResultsAsync(month, ct)).ToDictionary(r => r.EmployeeId, r => r.Commission)
            : [];
        var adjusts = await AdjustsAsync(month, ids, ct);

        return profiles.Select(p =>
        {
            var d = days.GetValueOrDefault(p.Id) is { } w
                ? new StaffWorkDays(w.Worked, w.PaidLeave, w.UnpaidLeave, w.Absent, w.Late, w.NoCheckOut, w.HasTimesheet)
                : StaffWorkDays.NoTimesheet;
            var mine = adjusts[p.Id].ToList();
            var slip = StaffPayCalculator.Calculate(new StaffContract(Money.FromTrusted(p.BaseSalary), Money.FromTrusted(p.Allowance), p.Probation), d,
                Money.FromTrusted(Math.Max(0, commission.GetValueOrDefault(p.Id))), mine.Select(a => a.Amount), Rules);
            return new StaffPayDto(p.Id, p.Code, p.FullName, p.Department, p.BranchId, d.Worked, d.PaidLeave, d.UnpaidLeave, d.Absent, d.Late,
                d.NoCheckOut, d.HasTimesheet, slip.PayDays, Rules.StandardDays, slip.Base.Amount, slip.BasePay.Amount, slip.Allowance.Amount,
                slip.Commission.Amount, slip.Adjust, mine, slip.Gross, slip.Fines.Amount, slip.Insurance.Amount, slip.Tax.Amount, slip.Net.Amount);
        }).ToList();
    }

    /// <summary>Điều chỉnh tháng của các người, kèm tên người nhập</summary>
    public async Task<ILookup<Guid, PayAdjustDto>> AdjustsAsync(string month, IReadOnlyCollection<Guid> employeeIds, CancellationToken ct)
    {
        var list = await db.PayAdjustments.AsNoTracking().Where(a => a.Month == month && employeeIds.Contains(a.EmployeeId))
            .OrderBy(a => a.Id).ToListAsync(ct);
        var by = await names.OfAsync(list.Select(a => (Guid?)a.By), ct);
        return list.Select(a => new PayAdjustDto(a.Id.Value, a.Month, a.EmployeeId, a.Amount, a.Reason, by.GetValueOrDefault(a.By, "—")))
            .ToLookup(a => a.EmployeeId);
    }

    private static TeacherPayDto Teacher(PayProfile p, Guid branchId, IReadOnlyList<TaughtSession> sessions, IReadOnlyList<PayAdjustDto> adjusts, bool withBase)
    {
        var rates = new TeacherRates(Money.FromTrusted(p.RateOffline), Money.FromTrusted(p.RateOnline));
        var taught = sessions.Where(s => !s.Cancelled).ToList();
        var slip = TeacherPayCalculator.Calculate(rates, withBase ? Money.FromTrusted(p.BaseSalary) : Money.Zero,
            taught.Select(s => new TeachingSession(s.SessionId, s.ClassId, s.Date, RoleOf(s.Role), s.Online, s.Minutes, s.Late, s.Confirmed)),
            adjusts.Select(a => a.Amount), Rules);
        var items = taught.Select(s =>
        {
            var rate = rates.For(s.Online).Amount;
            var bonus = RoleOf(s.Role) == TeachingRole.Substitute ? Rules.SubstituteBonus : 0;
            return new TeachItemDto(s.SessionId, s.ClassId, s.ClassCode, s.ClassName, s.BranchId, s.Date, TimeText.Of(s.StartTime), TimeText.Of(s.EndTime), s.Role,
                s.Online, TimeText.Of(s.CheckIn), TimeText.Of(s.CheckOut), s.Minutes, rate,
                decimal.Round(Math.Max(0, s.Minutes) / 60m * rate, 0, MidpointRounding.AwayFromZero), s.Confirmed, s.Late, bonus);
        }).ToList();
        return new TeacherPayDto(p.Id, p.Code, p.FullName, p.Kind, branchId, items, slip.Lines.Count, slip.Pending.Count,
            sessions.Count(s => s.Cancelled), slip.Hours, slip.Base.Amount, slip.Teaching.Amount, slip.SubstituteBonus.Amount, slip.LateFine.Amount,
            slip.Adjust, adjusts, slip.Total.Amount);
    }

    private static TeachingRole RoleOf(string role) => role switch
    {
        "substitute" => TeachingRole.Substitute,
        "assistant" => TeachingRole.Assistant,
        _ => TeachingRole.Main,
    };
}
