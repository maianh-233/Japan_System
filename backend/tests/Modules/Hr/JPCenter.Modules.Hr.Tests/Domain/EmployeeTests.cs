using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Tests.Domain;

public sealed class EmployeeTests
{
    private static readonly DateOnly Today = new(2026, 6, 1);

    private static EmploymentContract Contract(ContractType type = ContractType.Probation, DateOnly? end = null) =>
        new(type, "HD1", new DateOnly(2026, 1, 1), end ?? (type == ContractType.Indefinite ? null : new DateOnly(2026, 12, 31)), null, null);

    private static Result<Employee> Create(string name = "Nguyễn An", string email = "an@jpcenter.vn", EmployeeKind kind = EmployeeKind.Staff,
        decimal leave = 12, EmploymentContract? contract = null, EmployeeStatus? status = null) =>
        Employee.Create("NV001", name, Gender.Male, null, "0900000000", email, BranchId.New(), kind, Department.Sales, "Sale", true,
            new DateOnly(2026, 1, 1), null, contract ?? Contract(), leave, status);

    private static TeacherProfile Profile(params JlptLevel[] levels) =>
        new(TeacherNationality.Vn, [.. levels], [], Money.FromTrusted(200_000), Money.FromTrusted(150_000), [], [], null);

    [Fact]
    public void Can_ho_ten_va_email()
    {
        Create(name: " ").Error.Code.ShouldBe("hr.employee.name_required");
        Create(email: "").Error.Code.ShouldBe("hr.employee.email_required");
    }

    [Fact]
    public void Tao_moi_trang_thai_theo_hop_dong_va_khong_cho_nghi()
    {
        Create().Value.Status.ShouldBe(EmployeeStatus.Probation);
        Create(contract: Contract(ContractType.Indefinite)).Value.Status.ShouldBe(EmployeeStatus.Active);
        Create(status: EmployeeStatus.Quit).Error.Code.ShouldBe(EmployeeErrors.StatusNotAllowed.Code);
        Create(status: EmployeeStatus.Leave).Error.Code.ShouldBe(EmployeeErrors.StatusNotAllowed.Code);
    }

    [Fact]
    public void Hop_dong_co_thoi_han_can_ngay_ket_thuc_hop_le()
    {
        var noEnd = new EmploymentContract(ContractType.Fixed, "HD1", new DateOnly(2026, 1, 1), null, null, null);
        Create(contract: noEnd).Error.Code.ShouldBe(EmployeeErrors.ContractEndRequired.Code);
        Create(contract: Contract(ContractType.Fixed, new DateOnly(2025, 12, 1))).Error.Code.ShouldBe(EmployeeErrors.ContractDates.Code);
    }

    [Fact]
    public void Giao_vien_luon_phong_giang_day_khong_lam_ca()
    {
        var t = Create(kind: EmployeeKind.Teacher).Value;
        (t.Department, t.ShiftBased).ShouldBe((Department.Teaching, false));
        t.SetTeacherProfile(Profile()).Error.Code.ShouldBe(EmployeeErrors.LevelsRequired.Code);
        Create().Value.SetTeacherProfile(Profile(JlptLevel.N5)).Error.Code.ShouldBe(EmployeeErrors.NotTeacher.Code);
    }

    [Fact]
    public void Sua_ho_so_giu_lich_ranh()
    {
        var t = Create(kind: EmployeeKind.Teacher).Value;
        t.SetTeacherProfile(Profile(JlptLevel.N5)).IsSuccess.ShouldBeTrue();
        t.SetAvailability(new() { [1] = [ShiftKey.Morning, ShiftKey.Morning], [9] = [ShiftKey.Morning] }).IsSuccess.ShouldBeTrue();
        t.Teacher!.Availability.Keys.ShouldBe([1]);
        t.Teacher.Availability[1].ShouldBe([ShiftKey.Morning]);

        t.SetTeacherProfile(Profile(JlptLevel.N4, JlptLevel.N3)).IsSuccess.ShouldBeTrue();
        t.Teacher.Levels.ShouldBe([JlptLevel.N4, JlptLevel.N3]);
        t.Teacher.Availability[1].ShouldBe([ShiftKey.Morning]);
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
    public void Gia_han_hop_dong_luu_lich_su_va_chinh_thuc_hoa()
    {
        var e = Create().Value;
        var old = e.Contract;
        e.RenewContract(Contract(ContractType.Fixed, new DateOnly(2027, 12, 31)), DateTimeOffset.UtcNow, null).IsSuccess.ShouldBeTrue();
        e.Status.ShouldBe(EmployeeStatus.Active);
        e.ContractHistory.Count.ShouldBe(1);
        (e.ContractHistory[0].Type, e.ContractHistory[0].No).ShouldBe((old.Type, old.No));
        e.RenewContract(new EmploymentContract(ContractType.Fixed, "HD2", Today, null, null, null), DateTimeOffset.UtcNow, null)
            .Error.Code.ShouldBe(EmployeeErrors.ContractEndRequired.Code);
    }

    [Fact]
    public void Doi_trang_thai_khong_duoc_sang_nghi_viec()
    {
        var e = Create().Value;
        e.ChangeStatus(EmployeeStatus.Quit).Error.Code.ShouldBe(EmployeeErrors.StatusNotAllowed.Code);
        e.ChangeStatus(EmployeeStatus.Leave).IsSuccess.ShouldBeTrue();
        e.Status.ShouldBe(EmployeeStatus.Leave);
        e.ChangeStatus(EmployeeStatus.Probation).IsSuccess.ShouldBeTrue();
        e.Status.ShouldBe(EmployeeStatus.Probation);
    }

    [Fact]
    public void Nghi_dai_han_va_quay_lai()
    {
        var e = Create().Value;
        e.ReturnFromLeave().Error.Code.ShouldBe(EmployeeErrors.InvalidStatus.Code);
        e.StartLongLeave().IsSuccess.ShouldBeTrue();
        e.StartLongLeave().Error.Code.ShouldBe(EmployeeErrors.InvalidStatus.Code);
        e.ReturnFromLeave().IsSuccess.ShouldBeTrue();
        e.Status.ShouldBe(EmployeeStatus.Active);
    }

    [Fact]
    public void Nghi_viec_ngay_khi_ngay_nghi_da_toi()
    {
        var e = Create().Value;
        e.Quit(Today, " ", Today).Error.Code.ShouldBe(EmployeeErrors.QuitReasonRequired.Code);
        e.Quit(Today, "Chuyển công tác", Today).Value.ShouldBeTrue();
        (e.Status, e.QuitAt, e.QuitReason).ShouldBe((EmployeeStatus.Quit, Today, "Chuyển công tác"));
        e.Quit(Today, "x", Today).Error.Code.ShouldBe(EmployeeErrors.AlreadyQuit.Code);
        e.Update("A", Gender.Male, null, "1", "a@x.vn", e.BranchId, Department.Sales, "Sale", false, null)
            .Error.Code.ShouldBe(EmployeeErrors.AlreadyQuit.Code);
    }

    [Fact]
    public void Nghi_viec_hen_ngay_job_doi_trang_thai_khi_toi_ngay()
    {
        var e = Create().Value;
        var at = Today.AddDays(10);
        e.Quit(at, "Hết hợp đồng", Today).Value.ShouldBeFalse();
        (e.Status, e.QuitScheduled).ShouldBe((EmployeeStatus.Probation, true));
        e.ApplyScheduledQuit(Today.AddDays(9)).ShouldBeFalse();
        e.ApplyScheduledQuit(at).ShouldBeTrue();
        (e.Status, e.HasQuit, e.QuitScheduled).ShouldBe((EmployeeStatus.Quit, true, false));
        e.ApplyScheduledQuit(at).ShouldBeFalse();
    }
}
