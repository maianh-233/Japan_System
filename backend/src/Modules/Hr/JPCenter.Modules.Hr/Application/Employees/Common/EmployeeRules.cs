using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Employees.Common;

/// <summary>Kiểm tra dùng chung khi thêm / sửa hồ sơ + dựng hồ sơ giảng dạy, hợp đồng, mã nhân viên.</summary>
public sealed class EmployeeRules(IHrDbContext db, IBranchDirectory branches)
{
    /// <summary>Mã theo loại: NV / GV / TG + 4 số, tăng dần (NS-03). Trùng hiếm khi 2 người tạo cùng lúc → UQ code chặn, tạo lại.</summary>
    public async Task<string> NextCodeAsync(EmployeeKind kind, CancellationToken ct)
    {
        var prefix = kind switch { EmployeeKind.Teacher => "GV", EmployeeKind.Assistant => "TG", _ => "NV" };
        var codes = await db.Employees.IgnoreQueryFilters().Where(e => e.Code.StartsWith(prefix)).Select(e => e.Code).ToListAsync(ct);
        var max = codes.Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:0000}";
    }

    /// <summary>NS-03.E3 — email là email đăng nhập (PQ-08) nên không trùng, kể cả người đã nghỉ</summary>
    public async Task<Result> EnsureEmailUniqueAsync(string email, EmployeeId? except, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var taken = await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.Email == normalized && (except == null || e.Id != except), ct);
        return taken ? EmployeeErrors.DuplicateEmail : Result.Success();
    }

    /// <summary>NS-03.E7 — chi nhánh chính phải còn mở (đang hoạt động hoặc đang chuẩn bị — tuyển trước khai trương)</summary>
    public async Task<Result> EnsureBranchAsync(Guid branchId, CancellationToken ct)
    {
        var b = await branches.FindAsync(branchId, ct);
        return b is null || !b.AcceptsNewData ? EmployeeErrors.BranchInactive : Result.Success();
    }

    /// <summary>
    /// NS-03.E9 quản lý phải đang làm việc · NS-04.E1 không tự quản lý mình · NS-04.E2 không chọn cấp dưới của mình (tạo vòng).
    /// </summary>
    public async Task<Result> EnsureManagerAsync(Employee? self, Guid? managerId, CancellationToken ct)
    {
        if (managerId is not { } id) return Result.Success();
        if (self is not null && self.Id.Value == id) return EmployeeErrors.SelfManager;
        var all = await db.Employees.AsNoTracking().ToListAsync(ct);
        var manager = all.FirstOrDefault(e => e.Id.Value == id);
        if (manager is null || manager.HasQuit) return EmployeeErrors.ManagerInvalid;
        if (self is not null && EmployeeAccess.Subordinates(all, self.Id).Contains(manager.Id)) return EmployeeErrors.ManagerCycle;
        return Result.Success();
    }

    public static Result<EmploymentContract> Contract(ContractInput c, string no)
    {
        Money? Amount(decimal? v) => v is { } x ? Money.FromTrusted(Math.Round(x)) : null;
        var contract = new EmploymentContract(c.Type, no, c.Start, c.Type == ContractType.Indefinite ? null : c.End, Amount(c.BaseSalary), Amount(c.Allowance));
        return Employee.ValidateContract(contract) is { IsFailure: true } bad ? bad.Error : contract;
    }

    /// <summary>Số hợp đồng tự sinh: HĐ-{năm}/{mã NV}-{lần}</summary>
    public static string ContractNo(string code, int year, int sequence) => $"HĐ-{year}/{code}-{sequence:00}";

    public static TeacherProfile Teacher(TeacherInput t, Guid mainBranch) => new(
        t.Type,
        t.Levels.Select(EmployeeFieldsValidator.ParseLevel).OfType<JlptLevel>().Distinct().OrderBy(l => l).ToList(),
        (t.Certificates ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList(),
        Money.FromTrusted(Math.Round(t.Rate.Offline)), Money.FromTrusted(Math.Round(t.Rate.Online)),
        [],
        (t.ExtraBranchIds ?? []).Where(b => b != mainBranch).Distinct().Select(b => new BranchId(b)).ToList(),
        string.IsNullOrWhiteSpace(t.Bio) ? null : t.Bio.Trim());
}
