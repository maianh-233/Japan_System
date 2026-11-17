using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Branches;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Branches.Common;

/// <summary>Đọc chi nhánh kèm tên quản lý (Hr), số nhân sự / giáo viên (Hr), version (<c>xmin</c>).</summary>
public sealed class BranchReader(ISystemDbContext db, IEmployeeDirectory employees)
{
    public async Task<IReadOnlyList<BranchDto>> ListAsync(CancellationToken ct, BranchId? only = null)
    {
        var query = db.Branches.AsNoTracking();
        if (only is { } id) query = query.Where(b => b.Id == id);
        var rows = await query.OrderBy(b => b.Code).Select(b => new { Branch = b, Version = EF.Property<uint>(b, "xmin") }).ToListAsync(ct);

        var headcount = await employees.HeadcountByBranchAsync(ct);
        var managerIds = rows.Select(r => r.Branch.ManagerId).OfType<Guid>().ToList();
        var managers = await employees.FindManyAsync(managerIds, ct);

        return rows.Select(r =>
        {
            var b = r.Branch;
            var count = headcount.GetValueOrDefault(b.Id.Value);
            // Quản lý đã nghỉ việc → coi như chưa có (CN-01.E1)
            var manager = b.ManagerId is { } m && managers.TryGetValue(m, out var e) && !e.HasQuit ? e : null;
            return new BranchDto(b.Id.Value, b.Code, b.Name, b.Address, b.Phone, b.Email, manager?.Id, manager?.FullName, b.OpenHours,
                StatusText(b.Status), b.OpenedAt,
                b.BankAccount is null ? null : new BankDto(b.BankName!, b.BankAccount, b.BankHolder!),
                b.InvoicePrefix, b.Note,
                // ⏳ Phòng, lớp, học viên, thu — khi có module Đào tạo / Học viên / Tài chính
                new BranchStatsDto(count?.Staff ?? 0, count?.Teachers ?? 0, null, null, null, null, null),
                r.Version);
        }).ToList();
    }

    public async Task<BranchDto> GetAsync(BranchId id, CancellationToken ct) => (await ListAsync(ct, id)).Single();

    public static string StatusText(BranchStatus status) => status switch
    {
        BranchStatus.Preparing => "preparing",
        BranchStatus.Closed => "closed",
        _ => "active",
    };
}
