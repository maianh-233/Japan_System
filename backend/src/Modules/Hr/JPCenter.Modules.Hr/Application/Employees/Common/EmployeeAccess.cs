using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Employees.Common;

/// <summary>
/// Ai thấy hồ sơ nào (PQ-10) và có được xem lương không (NS-05.E3). Theo phạm vi quyền <c>view</c> của trang:
/// <c>all</c> mọi người · <c>branch</c> chi nhánh chính thuộc chi nhánh được gán (GV: cả chi nhánh dạy thêm) ·
/// <c>own</c> chính mình + cấp dưới theo cây quản lý. Ngoài phạm vi → coi như không tồn tại (404 — PQ-10.E2).
/// <para>Lọc trong bộ nhớ: số nhân sự 1 trung tâm nhỏ (vài trăm); không dùng global query filter vì 1 bảng phục vụ 2 trang khác mã quyền.</para>
/// </summary>
public sealed class EmployeeAccess(IHrDbContext db, ICurrentUser user, IClock clock)
{
    public const int ContractWarningDays = 45;

    public bool CanSeeSalary(EmployeeGroup group) =>
        user.Can(group.Page(), PermissionAction.Update) || user.Can(group.PayrollPage(), PermissionAction.View);

    public static readonly Error BranchNotAllowed = Error.Forbidden("hr.employee.branch_not_allowed",
        "Bạn chỉ thêm / chuyển nhân sự vào chi nhánh mình được phân công");

    /// <summary>Người thao tác có quyền <paramref name="action"/> tại chi nhánh này không (phạm vi branch / own chỉ trong chi nhánh được gán).</summary>
    public bool CanUseBranch(EmployeeGroup group, PermissionAction action, Guid branchId)
    {
        var perm = user.Permission(group.Page(), action);
        if (perm is null) return false;
        if (perm.Scope == PermissionScope.All) return true;
        if (perm.BranchIds.Count > 0) return perm.BranchIds.Contains(branchId);
        var me = user.Access;
        return me is not null && (me.AllBranches || me.BranchIds.Contains(branchId));
    }

    /// <summary>Mọi hồ sơ (kể cả nghỉ việc) kèm version — chưa lọc phạm vi.</summary>
    public async Task<List<(Employee Employee, uint Version)>> LoadAllAsync(CancellationToken ct)
    {
        var rows = await db.Employees.AsNoTracking()
            .Select(e => new { Employee = e, Version = EF.Property<uint>(e, "xmin") })
            .ToListAsync(ct);
        return rows.Select(r => (r.Employee, r.Version)).ToList();
    }

    /// <summary>Lọc theo nhóm trang + phạm vi quyền xem của người đang gọi.</summary>
    public IEnumerable<(Employee Employee, uint Version)> InScope(IReadOnlyList<(Employee Employee, uint Version)> all, EmployeeGroup group)
    {
        var mine = all.Where(x => group.Contains(x.Employee.Kind));
        var perm = user.Permission(group.Page(), PermissionAction.View);
        if (perm is null) return [];
        switch (perm.Scope)
        {
            case PermissionScope.All:
                return mine;
            case PermissionScope.Branch:
                if (perm.BranchIds.Count == 0) return mine; // gán "mọi chi nhánh"
                var branches = perm.BranchIds.ToHashSet();
                return mine.Where(x => branches.Contains(x.Employee.BranchId.Value)
                    || x.Employee.Teacher?.ExtraBranchIds.Any(b => branches.Contains(b.Value)) == true);
            default:
                // own: chính mình + mọi cấp dưới (trực tiếp / gián tiếp)
                var me = user.Access?.EmployeeId;
                if (me is null) return [];
                var visible = Subordinates(all.Select(x => x.Employee).ToList(), new EmployeeId(me.Value));
                visible.Add(new EmployeeId(me.Value));
                return mine.Where(x => visible.Contains(x.Employee.Id));
        }
    }

    /// <summary>Mọi cấp dưới (trực tiếp + gián tiếp) của 1 người — dùng cho phạm vi own và chặn vòng quản lý (NS-04.E2).</summary>
    public static HashSet<EmployeeId> Subordinates(IReadOnlyList<Employee> all, EmployeeId root)
    {
        var byManager = all.Where(e => e.ManagerId is not null).ToLookup(e => e.ManagerId!.Value);
        var result = new HashSet<EmployeeId>();
        var queue = new Queue<EmployeeId>([root]);
        while (queue.TryDequeue(out var id))
            foreach (var child in byManager[id])
                if (result.Add(child.Id)) queue.Enqueue(child.Id);
        return result;
    }

    /// <summary>Hồ sơ trong phạm vi (không theo dõi) — null nếu không có / ngoài phạm vi / khác nhóm.</summary>
    public async Task<(Employee Employee, uint Version)?> FindVisibleAsync(Guid id, EmployeeGroup group, CancellationToken ct)
    {
        var all = await LoadAllAsync(ct);
        var hit = InScope(all, group).FirstOrDefault(x => x.Employee.Id.Value == id);
        return hit.Employee is null ? null : hit;
    }

    /// <summary>Hồ sơ trong phạm vi, được theo dõi để sửa — null nếu không thấy.</summary>
    public async Task<Employee?> FindForUpdateAsync(Guid id, EmployeeGroup group, CancellationToken ct)
    {
        if (await FindVisibleAsync(id, group, ct) is null) return null;
        return await db.Employees.FirstOrDefaultAsync(e => e.Id == new EmployeeId(id), ct);
    }

    public int? ContractEndsIn(Employee e) => e.Contract.End is { } end ? end.DayNumber - clock.Today.DayNumber : null;

    public EmployeeDto ToDto(Employee e, uint version, IReadOnlyDictionary<EmployeeId, string> names, bool canSeeSalary, bool withHistory = false)
    {
        ContractDto Contract(EmploymentContract c) => new(c.Type, c.No, c.Start, c.End,
            canSeeSalary ? c.BaseSalary?.Amount : null, canSeeSalary ? c.Allowance?.Amount : null);

        var teacher = e.Teacher is { } t
            ? new TeacherDto(t.Nationality, t.Levels.Select(l => l.ToString()).ToList(), t.Certificates,
                canSeeSalary ? new RateDto(t.RateOffline.Amount, t.RateOnline.Amount) : null,
                t.Availability, t.ExtraBranchIds.Select(b => b.Value).ToList(), t.Bio)
            : null;

        return new EmployeeDto(e.Id.Value, e.Code, e.FullName, e.Gender, e.Dob, e.Phone, e.Email, e.BranchId.Value, e.Kind, e.Department,
            e.Position, e.ShiftBased, e.Status, e.JoinedAt, e.QuitAt, e.QuitReason, e.QuitScheduled,
            e.ManagerId?.Value, e.ManagerId is { } m ? names.GetValueOrDefault(m) : null,
            Contract(e.Contract), ContractEndsIn(e), e.LeaveBalance, e.HasAccount, teacher, e.Note, !canSeeSalary, version,
            withHistory ? e.ContractHistory.Select(h => Contract(new EmploymentContract(h.Type, h.No, h.Start, h.End, h.BaseSalary, h.Allowance))).ToList() : null);
    }

    /// <summary>Hồ sơ chi tiết sau khi lưu (không lọc phạm vi — người vừa thao tác đã được kiểm trước đó)</summary>
    public async Task<EmployeeDto> DetailAsync(EmployeeId id, EmployeeGroup group, CancellationToken ct)
    {
        var all = await LoadAllAsync(ct);
        var self = all.Single(x => x.Employee.Id == id);
        return ToDto(self.Employee, self.Version, Names(all), CanSeeSalary(group), withHistory: true);
    }

    /// <summary>Tên mọi nhân sự (để hiện tên quản lý)</summary>
    public static IReadOnlyDictionary<EmployeeId, string> Names(IEnumerable<(Employee Employee, uint Version)> all) =>
        all.ToDictionary(x => x.Employee.Id, x => x.Employee.FullName);
}
