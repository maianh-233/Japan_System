using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Paging;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.GetEmployees;

/// <summary>NS-01 (nhân viên) / NS-02 (giáo viên & trợ giảng) — danh sách trong phạm vi quyền + số liệu thẻ + số theo nhóm.</summary>
/// <param name="BranchId">Chi nhánh đang chọn ở thanh trên cùng (GV: tính cả chi nhánh dạy thêm — NS-02.E2)</param>
/// <param name="Group">Nhóm chip: phòng ban (nhân viên) · "vn" / "jp" / "assistant" (giáo viên)</param>
/// <param name="Level">Dạy cấp "N5"…"N1" (giáo viên)</param>
/// <param name="Special">"ending" hợp đồng ≤ 45 ngày · "no_account" chưa có tài khoản</param>
/// <param name="IncludeQuit">Hiện người đã nghỉ (mặc định ẩn)</param>
public sealed record GetEmployeesQuery(
    EmployeeGroup Scope, Guid? BranchId, string? Group, EmployeeStatus? Status, string? Level, string? Special, bool IncludeQuit, string? Q,
    int Page = 1, int PageSize = 100) : IQuery<EmployeeListResult>;

public sealed class GetEmployeesHandler(EmployeeAccess access) : IQueryHandler<GetEmployeesQuery, EmployeeListResult>
{
    public async Task<Result<EmployeeListResult>> Handle(GetEmployeesQuery q, CancellationToken ct)
    {
        var all = await access.LoadAllAsync(ct);
        var names = EmployeeAccess.Names(all);
        var rows = access.InScope(all, q.Scope)
            .Where(x => q.BranchId is not { } b || x.Employee.BranchId.Value == b || x.Employee.Teacher?.ExtraBranchIds.Any(e => e.Value == b) == true)
            .ToList();

        // Thẻ số liệu: không tính người đã nghỉ (giống frontend)
        var live = rows.Where(x => !x.Employee.HasQuit).ToList();
        var stats = new EmployeeStats(
            live.Count,
            live.Count(x => x.Employee.Status == EmployeeStatus.Probation),
            live.Count(x => access.ContractEndsIn(x.Employee) is <= EmployeeAccess.ContractWarningDays),
            live.Count(x => !x.Employee.HasAccount));

        var level = q.Level is null ? null : Common.EmployeeFieldsValidator.ParseLevel(q.Level);
        var filtered = rows.Where(x =>
        {
            var e = x.Employee;
            if (!q.IncludeQuit && e.HasQuit && q.Status != EmployeeStatus.Quit) return false;
            if (q.Status is { } s && e.Status != s) return false;
            if (level is { } l && e.Teacher?.Levels.Contains(l) != true) return false;
            if (q.Special == "ending" && (e.HasQuit || access.ContractEndsIn(e) is not <= EmployeeAccess.ContractWarningDays)) return false;
            if (q.Special == "no_account" && (e.HasQuit || e.HasAccount)) return false;
            return string.IsNullOrWhiteSpace(q.Q) || Matches(e, q.Q);
        }).ToList();

        var groups = filtered.GroupBy(x => GroupOf(x.Employee)).ToDictionary(g => g.Key, g => g.Count());
        groups["all"] = filtered.Count;
        if (!string.IsNullOrEmpty(q.Group) && q.Group != "all") filtered = filtered.Where(x => GroupOf(x.Employee) == q.Group).ToList();

        var page = new PageRequest(q.Page, q.PageSize);
        var canSeeSalary = access.CanSeeSalary(q.Scope);
        var items = filtered
            .OrderBy(x => x.Employee.HasQuit).ThenBy(x => x.Employee.FullName, StringComparer.Create(new global::System.Globalization.CultureInfo("vi-VN"), false))
            .Skip((page.SafePage - 1) * page.SafePageSize).Take(page.SafePageSize)
            .Select(x => access.ToDto(x.Employee, x.Version, names, canSeeSalary))
            .ToList();
        return new EmployeeListResult(items, filtered.Count, page.SafePage, page.SafePageSize, stats, groups);
    }

    /// <summary>Nhóm chip: phòng ban (nhân viên) · vn / jp / assistant (giáo viên)</summary>
    public static string GroupOf(Employee e) => e.Kind switch
    {
        EmployeeKind.Assistant => "assistant",
        EmployeeKind.Teacher => e.Teacher?.Nationality == TeacherNationality.Jp ? "jp" : "vn",
        _ => global::System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(e.Department.ToString()),
    };

    private static bool Matches(Employee e, string q)
    {
        var needle = Fold(q);
        return Fold($"{e.FullName} {e.Code} {e.Phone} {e.Email} {e.Position}").Contains(needle, StringComparison.Ordinal);
    }

    private static string Fold(string s)
    {
        var d = s.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(global::System.Text.NormalizationForm.FormD);
        return new string(d.Where(c => global::System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            != global::System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }
}
