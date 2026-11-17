using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Tests.Domain;

public sealed class EmployeeTests
{
    private static Result<Employee> Create(string name = "Nguyễn An", string email = "an@jpcenter.vn", EmployeeKind kind = EmployeeKind.Staff,
        decimal leave = 12) =>
        Employee.Create("NV001", name, Gender.Male, null, "0900000000", email, BranchId.New(), kind, Department.Sales, "Sale", false,
            new DateOnly(2026, 1, 1), null, new EmploymentContract(ContractType.Probation, "HD1", new DateOnly(2026, 1, 1), null, null, null), leave);

    [Fact]
    public void Can_ho_ten_va_email()
    {
        Create(name: " ").Error.Code.ShouldBe("hr.employee.name_required");
        Create(email: "").Error.Code.ShouldBe("hr.employee.email_required");
    }

    [Fact]
    public void Khong_la_quan_ly_cua_chinh_minh()
    {
        var e = Create().Value;
        e.SetManager(e.Id).Error.Code.ShouldBe("hr.employee.self_manager");
        e.SetManager(EmployeeId.New()).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Dung_phep_khong_vuot_so_ngay_con()
    {
        var e = Create(leave: 2).Value;
        e.UseLeave(1.5m).IsSuccess.ShouldBeTrue();
        e.UseLeave(1m).Error.Code.ShouldBe("hr.employee.leave_exceeded");
        e.LeaveBalance.ShouldBe(0.5m);
    }

    [Fact]
    public void Nghi_viec_va_nghi_dai_han()
    {
        var e = Create().Value;
        e.StartLongLeave();
        e.Status.ShouldBe(EmployeeStatus.Leave);
        e.ReturnFromLeave();
        e.Status.ShouldBe(EmployeeStatus.Active);
        e.Quit(new DateOnly(2026, 12, 31));
        (e.Status, e.QuitAt).ShouldBe((EmployeeStatus.Quit, new DateOnly(2026, 12, 31)));
    }
}
