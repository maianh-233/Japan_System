using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp nhân viên mẫu từ <c>hr.seed.json</c> (xuất từ mock frontend). Chỉ chạy khi bảng nhân viên còn trống — không xóa gì.
/// </summary>
internal sealed class HrSeeder(HrDbContext db) : IDataSeeder
{
    public int Order => 10;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<HrSeedFile>(typeof(HrSeeder).Assembly, "hr.seed.json");

        var created = new List<(Employee Entity, SeedEmployee Source)>();
        foreach (var s in data.Employees)
        {
            var contract = new EmploymentContract(
                SnakeCaseEnumConverter<ContractType>.Parse(s.Contract.Type), s.Contract.No ?? "",
                DateOnly.Parse(s.Contract.Start), s.Contract.End is null ? null : DateOnly.Parse(s.Contract.End),
                s.Contract.BaseSalary is { } bs ? Money.FromTrusted(bs) : null,
                s.Contract.Allowance is { } al ? Money.FromTrusted(al) : null);

            var employee = Employee.Create(s.Code, s.FullName, SnakeCaseEnumConverter<Gender>.Parse(s.Gender),
                s.Dob is null ? null : DateOnly.Parse(s.Dob), s.Phone, s.Email, new BranchId(SeedData.IdOf(s.BranchId)),
                SnakeCaseEnumConverter<EmployeeKind>.Parse(s.Kind), SnakeCaseEnumConverter<Department>.Parse(s.Department),
                s.Position, s.ShiftBased, DateOnly.Parse(s.JoinedAt), null, contract, s.LeaveBalance).Value;

            if (s.Teacher is { } t)
            {
                employee.SetTeacherProfile(new TeacherProfile(
                    SnakeCaseEnumConverter<TeacherNationality>.Parse(t.Type),
                    t.Levels.Select(SnakeCaseEnumConverter<JlptLevel>.Parse).ToList(),
                    t.Certificates, Money.FromTrusted(t.Rate.Offline), Money.FromTrusted(t.Rate.Online),
                    (t.Availability ?? []).ToDictionary(kv => int.Parse(kv.Key, global::System.Globalization.CultureInfo.InvariantCulture),
                        kv => kv.Value.Select(SnakeCaseEnumConverter<ShiftKey>.Parse).ToList()),
                    (t.ExtraBranchIds ?? []).Select(b => new BranchId(SeedData.IdOf(b))).ToList(),
                    t.Bio));
            }

            switch (s.Status)
            {
                case "leave": employee.StartLongLeave(); break;
                case "quit": employee.MarkQuit(s.QuitAt is null ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(s.QuitAt), null); break;
            }
            if (s.HasAccount) employee.MarkHasAccount();

            db.AddWithId(employee, SeedData.IdOf(s.Id));
            created.Add((employee, s));
        }

        // Quản lý trực tiếp — gán sau khi đã có đủ nhân viên
        foreach (var (entity, source) in created.Where(x => x.Source.ManagerId is not null))
            entity.SetManager(new EmployeeId(SeedData.IdOf(source.ManagerId!)));

        await db.SaveChangesAsync(ct);
    }

    private sealed record HrSeedFile(List<SeedEmployee> Employees);

    private sealed record SeedEmployee(
        string Id, string Code, string FullName, string Gender, string? Dob, string Phone, string Email, string BranchId,
        string Kind, string Department, string Position, bool ShiftBased, string Status, string JoinedAt, string? QuitAt,
        string? ManagerId, SeedContract Contract, decimal LeaveBalance, bool HasAccount, SeedTeacher? Teacher);

    private sealed record SeedContract(string Type, string? No, string Start, string? End, decimal? BaseSalary, decimal? Allowance);

    private sealed record SeedTeacher(string Type, List<string> Levels, List<string> Certificates, SeedRate Rate,
        Dictionary<string, List<string>>? Availability, List<string>? ExtraBranchIds, string? Bio);

    private sealed record SeedRate(decimal Offline, decimal Online);
}
