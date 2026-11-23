using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Tests.Domain;

public sealed class TimeRecordTests
{
    private static readonly DateOnly Today = new(2026, 6, 10); // thứ 4
    private static readonly WorkWindow Office = new(new TimeOnly(8, 0), new TimeOnly(17, 30), "HC");
    private static TimeOnly T(int h, int m = 0) => new(h, m);

    private static AttendanceDay Eval(TimeOnly? i, TimeOnly? o, DateOnly? date = null, WorkWindow? exp = null, bool leave = false, bool excused = false,
        TimeOnly? now = null) =>
        AttendanceDay.Evaluate(date ?? Today.AddDays(-1), exp ?? Office, i, o, leave, excused, Today, now ?? T(10), graceMin: 5);

    [Fact]
    public void CC01_check_out_can_check_in_truoc_va_sau_gio_vao()
    {
        var r = TimeRecord.CheckInAt(EmployeeId.New(), BranchId.New(), Today, T(8, 2));
        r.CheckOutAt(T(7)).Error.Code.ShouldBe("hr.time_record.invalid_checkout");
        r.CheckOutAt(T(17, 35)).IsSuccess.ShouldBeTrue();
        r.CheckOutAt(T(18)).IsSuccess.ShouldBeTrue();
        r.CheckOut.ShouldBe(T(18));
    }

    [Fact]
    public void CC04_sua_cong_can_ly_do_khong_sua_ngay_tuong_lai()
    {
        var r = TimeRecord.CheckInAt(EmployeeId.New(), BranchId.New(), Today, T(8));
        r.Edit(T(8), T(17), Guid.NewGuid(), "HR", " ", Today).Error.Code.ShouldBe("hr.time_record.reason_required");
        r.Edit(T(9), T(8), Guid.NewGuid(), "HR", "quên", Today).Error.Code.ShouldBe("hr.time_record.invalid_checkout");
        r.Edit(T(8), T(17, 30), Guid.NewGuid(), "HR", " quên check-out ", Today).IsSuccess.ShouldBeTrue();
        r.EditReason.ShouldBe("quên check-out");
        TimeRecord.Manual(EmployeeId.New(), BranchId.New(), Today.AddDays(1), T(8), null, Guid.NewGuid(), "HR", "x", Today).Error.Code
            .ShouldBe("hr.time_record.future_date");
        TimeRecord.Manual(EmployeeId.New(), BranchId.New(), Today, null, T(17), Guid.NewGuid(), "HR", "x", Today).Value.CheckOut.ShouldBe(T(17));
    }

    [Fact]
    public void CC02_trang_thai_ngay()
    {
        Eval(T(8, 5), T(17, 30)).State.ShouldBe(AttendanceState.Ok); // muộn 5′ = trong ngưỡng
        Eval(T(8, 20), T(17, 30)).State.ShouldBe(AttendanceState.Late);
        Eval(T(8, 20), T(17, 30)).LateMin.ShouldBe(20);
        Eval(T(8), T(16)).State.ShouldBe(AttendanceState.Early);
        Eval(T(8, 30), T(16)).State.ShouldBe(AttendanceState.LateEarly);
        Eval(T(8, 30), T(16), excused: true).State.ShouldBe(AttendanceState.Ok);
        Eval(T(8), null).State.ShouldBe(AttendanceState.NoOut);
        Eval(null, null).State.ShouldBe(AttendanceState.Absent);
        Eval(null, null, leave: true).State.ShouldBe(AttendanceState.Leave);
        Eval(T(8), T(17, 30)).WorkedMin.ShouldBe(570);
    }

    [Fact]
    public void CC02_hom_nay_tuong_lai_va_khong_co_ca()
    {
        Eval(null, null, date: Today, now: T(9)).State.ShouldBe(AttendanceState.Future);
        Eval(null, null, date: Today, now: T(18)).State.ShouldBe(AttendanceState.Absent);
        Eval(T(8), null, date: Today).State.ShouldBe(AttendanceState.Working);
        Eval(null, null, date: Today.AddDays(1)).State.ShouldBe(AttendanceState.Future);
        AttendanceDay.Evaluate(Today, null, null, null, false, false, Today, T(9), 5).State.ShouldBe(AttendanceState.Off);
        AttendanceDay.Evaluate(Today.AddDays(-1), null, T(9), T(12), false, false, Today, T(9), 5).State.ShouldBe(AttendanceState.Ok); // làm bù
    }

    [Fact]
    public void CC02_gio_phai_lam_theo_ca_hoac_hanh_chinh()
    {
        var sunday = new DateOnly(2026, 6, 14);
        var saturday = new DateOnly(2026, 6, 13);
        AttendanceDay.ExpectedOf(sunday, false, [], T(8), T(17, 30), T(12)).ShouldBeNull();
        AttendanceDay.ExpectedOf(saturday, false, [], T(8), T(17, 30), T(12))!.To.ShouldBe(T(12));
        AttendanceDay.ExpectedOf(saturday, false, [], T(8), T(17, 30), null).ShouldBeNull();
        AttendanceDay.ExpectedOf(Today, true, [], T(8), T(17, 30), null).ShouldBeNull();
        var w = AttendanceDay.ExpectedOf(Today, true, [(T(17, 30), T(21, 30), "T"), (T(13), T(17, 30), "C")], T(8), T(17, 30), null)!;
        (w.From, w.To, w.Label).ShouldBe((T(13), T(21, 30), "C+T"));
    }
}
