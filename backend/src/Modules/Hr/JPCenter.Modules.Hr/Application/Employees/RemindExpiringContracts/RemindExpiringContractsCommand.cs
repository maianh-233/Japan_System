using JPCenter.Application.Common.Email;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Employees.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Employees.RemindExpiringContracts;

/// <summary>
/// NS-09.E1 — job hằng ngày: hợp đồng còn ĐÚNG 45 / 15 / 7 ngày → email người có quyền sửa hồ sơ (nhân viên: <c>hr.employees.update</c>;
/// GV: <c>hr.teachers.update</c>) tại chi nhánh chính của người đó + quản lý trực tiếp. Mỗi người nhận 1 email gom mọi hợp đồng.
/// </summary>
public sealed record RemindExpiringContractsCommand : ICommand<int>;

public sealed class RemindExpiringContractsHandler(
    IHrDbContext db, IStaffAccessDirectory access, IBranchDirectory branches, IEmailSender email, IAppLinks links, IClock clock)
    : ICommandHandler<RemindExpiringContractsCommand, int>
{
    public static readonly int[] RemindDays = [45, 15, 7];

    public async Task<Result<int>> Handle(RemindExpiringContractsCommand cmd, CancellationToken ct)
    {
        var today = clock.Today;
        var dates = RemindDays.Select(d => today.AddDays(d)).ToList();
        var all = await db.Employees.AsNoTracking().ToListAsync(ct);
        var due = all.Where(e => !e.HasQuit && e.Contract.End is { } end && dates.Contains(end)).ToList();
        if (due.Count == 0) return 0;

        var branchNames = (await branches.ListAsync(false, ct)).ToDictionary(b => b.Id, b => b.Name);
        var byId = all.ToDictionary(e => e.Id);
        // email người nhận → (tên, danh sách hợp đồng)
        var inbox = new Dictionary<string, (string? Name, List<EmployeeEmails.ExpiringItem> Items)>(StringComparer.OrdinalIgnoreCase);
        void Add(string to, string? name, EmployeeEmails.ExpiringItem item)
        {
            if (!inbox.TryGetValue(to, out var box)) inbox[to] = box = (name, []);
            if (!box.Items.Contains(item)) box.Items.Add(item);
        }

        foreach (var e in due)
        {
            var end = e.Contract.End!.Value;
            var item = new EmployeeEmails.ExpiringItem($"{e.FullName} · {e.Code}",
                $"{e.Position} · {branchNames.GetValueOrDefault(e.BranchId.Value, "?")}", end, end.DayNumber - today.DayNumber,
                links.Page(EmployeeGroups.ProfilePath(e.Kind, e.Id.Value)));

            var page = EmployeeGroups.Of(e.Kind).Page();
            foreach (var hr in await access.WithPermissionAsync($"{page}.update", e.BranchId.Value, ct))
                Add(hr.Email, byId.TryGetValue(new EmployeeId(hr.EmployeeId), out var me) ? me.FullName : null, item);
            if (e.ManagerId is { } m && byId.TryGetValue(m, out var manager) && !manager.HasQuit)
                Add(manager.Email, manager.FullName, item);
        }

        var listLink = links.Page("/admin/hr/employees");
        foreach (var (to, box) in inbox)
            await email.SendAsync(to, EmployeeEmails.ContractsExpiring(box.Name, box.Items, listLink), ct);
        return due.Count;
    }
}
