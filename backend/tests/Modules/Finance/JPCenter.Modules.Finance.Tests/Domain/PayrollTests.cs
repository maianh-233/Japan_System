using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.Finance.Domain.Payroll.Events;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Tests.Domain;

public sealed class PayrollTests
{
    private static readonly BranchId Branch = new(Guid.NewGuid());
    private static readonly Guid Accountant = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 12, 3, 3, 0, 0, TimeSpan.Zero);
    private static readonly PayrollRules Rules = PayrollRules.Default;
    private static readonly TeacherRates Rates = new(M(200_000), M(150_000));

    private static Money M(decimal v) => Money.FromTrusted(v);

    private static TeachingSession S(int minutes = 90, bool confirmed = true, bool online = false, TeachingRole role = TeachingRole.Main,
        bool late = false, int day = 5) =>
        new(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 11, day), role, online, minutes, late, confirmed);

    [Fact]
    public void TC_13_luong_GV_buoi_da_xac_nhan_theo_phut_va_don_gia()
    {
        var slip = TeacherPayCalculator.Calculate(Rates, Money.Zero,
            [S(90), S(60, online: true), S(90, confirmed: false)], [], Rules);

        slip.Lines.Count.ShouldBe(2);
        slip.Pending.ShouldHaveSingleItem();
        slip.Teaching.ShouldBe(M(300_000 + 150_000));
        slip.Hours.ShouldBe(2.5m);
        slip.Total.ShouldBe(M(450_000));
    }

    [Fact]
    public void TC_13_day_thay_cong_thuong_di_muon_tru_phat_cong_dieu_chinh_va_luong_cung()
    {
        var slip = TeacherPayCalculator.Calculate(Rates, M(3_000_000),
            [S(60, role: TeachingRole.Substitute), S(60, late: true)], [100_000, -20_000], Rules);

        slip.SubstituteBonus.ShouldBe(M(50_000));
        slip.LateFine.ShouldBe(M(30_000));
        slip.Adjust.ShouldBe(80_000);
        slip.Total.ShouldBe(M(3_000_000 + 400_000 + 50_000 - 30_000 + 80_000));
    }

    [Fact]
    public void TC_13_E6_ket_thuc_som_tinh_theo_phut_thuc_lam_tron_ve_dong()
    {
        var slip = TeacherPayCalculator.Calculate(new TeacherRates(M(185_000), M(0)), Money.Zero, [S(37)], [], Rules);
        slip.Teaching.ShouldBe(M(114_083)); // 37/60 × 185.000 = 114.083,33
    }

    [Fact]
    public void Luong_GV_am_ve_0()
    {
        TeacherPayCalculator.Calculate(Rates, Money.Zero, [], [-500_000], Rules).Total.ShouldBe(Money.Zero);
    }

    [Fact]
    public void TC_14_luong_NV_theo_cong_phu_cap_hoa_hong_BHXH_thue()
    {
        var slip = StaffPayCalculator.Calculate(new StaffContract(M(26_000_000), M(1_000_000), false),
            new StaffWorkDays(Worked: 24, PaidLeave: 1, UnpaidLeave: 1, Absent: 0, Late: 2, NoCheckOut: 1, HasTimesheet: true),
            M(2_000_000), [500_000], Rules);

        slip.PayDays.ShouldBe(25);
        slip.BasePay.ShouldBe(M(25_000_000));
        slip.Gross.ShouldBe(28_500_000);
        slip.Fines.ShouldBe(M(130_000));
        slip.Insurance.ShouldBe(M(2_730_000));
        // chịu thuế = 28.500.000 − 130.000 − 2.730.000 − 11.000.000 = 14.640.000
        slip.Tax.ShouldBe(PersonalIncomeTax.Of(14_640_000));
        slip.Net.ShouldBe(M(28_500_000 - 130_000 - 2_730_000 - slip.Tax.Amount));
    }

    [Fact]
    public void TC_14_cong_vuot_chuan_chi_tinh_bang_chuan_thu_viec_khong_dong_BHXH()
    {
        var slip = StaffPayCalculator.Calculate(new StaffContract(M(8_000_000), Money.Zero, true),
            new StaffWorkDays(27, 1, 0, 0, 0, 0, true), Money.Zero, [], Rules);
        slip.PayDays.ShouldBe(26);
        slip.BasePay.ShouldBe(M(8_000_000));
        slip.Insurance.ShouldBe(Money.Zero);
        slip.Tax.ShouldBe(Money.Zero);
    }

    [Fact]
    public void TC_14_E2_chua_co_cham_cong_tam_lay_cong_chuan()
    {
        var slip = StaffPayCalculator.Calculate(new StaffContract(M(10_000_000), Money.Zero, false), StaffWorkDays.NoTimesheet, Money.Zero, [], Rules);
        slip.PayDays.ShouldBe(26);
        slip.BasePay.ShouldBe(M(10_000_000));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5_000_000, 250_000)]
    [InlineData(14_640_000, 250_000 + 500_000 + 696_000)]
    [InlineData(100_000_000, 250_000 + 500_000 + 1_200_000 + 2_800_000 + 5_000_000 + 8_400_000 + 7_000_000)]
    public void Thue_TNCN_luy_tien(decimal taxable, decimal tax) => PersonalIncomeTax.Of(taxable).ShouldBe(M(tax));

    [Fact]
    public void TC_13_E1_dieu_chinh_bang_0_thieu_ly_do_sai_thang()
    {
        PayAdjustment.Create("2026-11", Guid.NewGuid(), 0, "x", Accountant).Error.ShouldBe(PayrollErrors.InvalidAdjustAmount);
        PayAdjustment.Create("2026-11", Guid.NewGuid(), 10.5m, "x", Accountant).Error.ShouldBe(PayrollErrors.InvalidAdjustAmount);
        PayAdjustment.Create("2026-11", Guid.NewGuid(), -50_000, "  ", Accountant).Error.ShouldBe(PayrollErrors.AdjustReasonRequired);
        PayAdjustment.Create("11/2026", Guid.NewGuid(), 50_000, "Thưởng", Accountant).Error.ShouldBe(PayrollErrors.InvalidMonth);
        PayAdjustment.Create(" 2026-11 ", Guid.NewGuid(), -50_000, " Đi muộn ", Accountant).Value.Reason.ShouldBe("Đi muộn");
    }

    [Fact]
    public void TC_13_chot_mo_khoa_chot_lai_da_chi()
    {
        var l = PayrollLock.Lock(PayrollKind.Teacher, "2026-11", Branch, M(10_000_000), "[]", Accountant, At).Value;
        l.IsLocked.ShouldBeTrue();
        l.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<PayrollLocked>().Total.ShouldBe(M(10_000_000));

        l.Relock(M(1), "[]", Accountant, At).Error.ShouldBe(PayrollErrors.Locked);
        l.Unlock(Accountant, At).IsSuccess.ShouldBeTrue();
        l.IsLocked.ShouldBeFalse();
        l.MarkPaid(At).Error.ShouldBe(PayrollErrors.NotLocked);
        l.Unlock(Accountant, At).Error.ShouldBe(PayrollErrors.NotLocked);

        l.Relock(M(12_000_000), "[{}]", Accountant, At.AddHours(1)).IsSuccess.ShouldBeTrue(); // TC-13.E4 ghi đè
        l.Total.ShouldBe(M(12_000_000));
        l.UnlockedAt.ShouldBeNull();

        l.MarkPaid(At.AddDays(1)).IsSuccess.ShouldBeTrue();
        l.MarkPaid(At.AddDays(2)).Error.ShouldBe(PayrollErrors.AlreadyPaid);
        l.Unlock(Accountant, At.AddDays(2)).Error.ShouldBe(PayrollErrors.AlreadyPaid); // TC-13.E3
    }

    [Fact]
    public void Thang_luong_va_han_chot()
    {
        PayrollLock.Lock(PayrollKind.Staff, "2026-13", Branch, Money.Zero, "[]", Accountant, At).Error.ShouldBe(PayrollErrors.InvalidMonth);
        PayrollMonth.Range("2026-02").ShouldBe((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)));
        PayrollMonth.LockDeadline("2026-12", 5).ShouldBe(new DateOnly(2027, 1, 5));
        PayrollMonth.IsLockOverdue("2026-11", 5, new DateOnly(2026, 12, 6), locked: false).ShouldBeTrue();
        PayrollMonth.IsLockOverdue("2026-11", 5, new DateOnly(2026, 12, 5), locked: false).ShouldBeFalse();
        PayrollMonth.IsLockOverdue("2026-11", 5, new DateOnly(2026, 12, 6), locked: true).ShouldBeFalse();
    }
}
