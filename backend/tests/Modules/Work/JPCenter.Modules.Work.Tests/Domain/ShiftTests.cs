using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Tests.Domain;

public sealed class ShiftTests
{
    private static readonly DateOnly Today = new(2026, 6, 10); // thứ 4
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void XC01_khong_xep_ngay_da_qua_va_ca_bao_ban()
    {
        ShiftAssignment.Create(Guid.NewGuid(), BranchId.New(), Today.AddDays(-1), ShiftKey.Morning, Today, false).Error.Code.ShouldBe("work.shift.past_date");
        ShiftAssignment.Create(Guid.NewGuid(), BranchId.New(), Today, ShiftKey.Morning, Today, busy: true).Error.Code.ShouldBe("work.shift.busy");
        var s = ShiftAssignment.Create(Guid.NewGuid(), BranchId.New(), Today, ShiftKey.Evening, Today, false).Value;
        s.CanRemove(Today).IsSuccess.ShouldBeTrue();
        s.CanRemove(Today.AddDays(1)).Error.Code.ShouldBe("work.shift.past_date");
        var other = Guid.NewGuid();
        s.SwapTo(other);
        s.EmployeeId.ShouldBe(other);
    }

    [Fact]
    public void XC03_dinh_muc_nguoi_moi_ca()
    {
        var saturday = new DateOnly(2026, 6, 13);
        ShiftCoverage.Minimum("sales", Today, ShiftKey.Evening).ShouldBe(2);
        ShiftCoverage.Minimum("sales", Today, ShiftKey.Morning).ShouldBe(1);
        ShiftCoverage.Minimum("sales", saturday, ShiftKey.Evening).ShouldBe(1);
        ShiftCoverage.Minimum("reception", Today, ShiftKey.Afternoon).ShouldBe(1);
        ShiftCoverage.Minimum("reception", Today, ShiftKey.Evening).ShouldBe(0);
        ShiftCoverage.Minimum("reception", saturday, ShiftKey.Morning).ShouldBe(1);
        ShiftCoverage.Minimum("reception", saturday, ShiftKey.Afternoon).ShouldBe(0);
        ShiftCoverage.Minimum("marketing", Today, ShiftKey.Morning).ShouldBe(0);
    }

    [Fact]
    public void XC04_bao_ranh_ban_khong_sua_ngay_da_qua()
    {
        ShiftRegistration.Create(Guid.NewGuid(), Today.AddDays(-1), ShiftKey.Morning, ShiftRegistrationKind.Busy, Now, Today).Error.Code.ShouldBe("work.shift.past_date");
        var r = ShiftRegistration.Create(Guid.NewGuid(), Today, ShiftKey.Morning, ShiftRegistrationKind.Available, Now, Today).Value;
        r.Change(ShiftRegistrationKind.Busy, Now, Today).IsSuccess.ShouldBeTrue();
        r.Kind.ShouldBe(ShiftRegistrationKind.Busy);
        r.Change(ShiftRegistrationKind.Available, Now, Today.AddDays(1)).Error.Code.ShouldBe("work.shift.past_date");
    }

    [Fact]
    public void Gio_ca_va_ca_ke_tiep()
    {
        WorkShiftTimes.Of(ShiftKey.Afternoon).ShouldBe((new TimeOnly(13, 0), new TimeOnly(17, 30), "C"));
        WorkShiftTimes.Next(ShiftKey.Morning).ShouldBe(ShiftKey.Afternoon);
        WorkShiftTimes.Next(ShiftKey.Evening).ShouldBeNull();
    }
}
