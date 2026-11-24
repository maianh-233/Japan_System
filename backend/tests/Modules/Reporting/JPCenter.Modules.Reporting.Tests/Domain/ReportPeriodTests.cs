using JPCenter.Modules.Reporting.Domain.Metrics;
using JPCenter.Modules.Reporting.Domain.Periods;

namespace JPCenter.Modules.Reporting.Tests.Domain;

/// <summary>User story 17 — kỳ báo cáo, tỷ lệ, công suất phòng</summary>
public sealed class ReportPeriodTests
{
    private static readonly DateOnly Today = new(2026, 2, 17);

    [Fact]
    public void BC03_ky_6_thang_tinh_tu_ngay_1_qua_nam()
    {
        var p = ReportPeriod.Create(6, Today).Value;
        (p.From, p.To, p.CurrentMonth, p.PreviousMonth).ShouldBe((new DateOnly(2025, 9, 1), Today, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));
        p.MonthStarts.Select(ReportPeriod.KeyOf).ShouldBe(["2025-09", "2025-10", "2025-11", "2025-12", "2026-01", "2026-02"]);
        p.MonthStarts.Select(p.LabelOf).ShouldBe(["T9/25", "T10/25", "T11/25", "T12/25", "T1", "T2"]);
        p.Contains(new DateOnly(2025, 8, 31)).ShouldBeFalse();
        p.Contains(new DateOnly(2025, 9, 1)).ShouldBeTrue();
        p.Contains(Today.AddDays(1)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(24)]
    public void BC03_E3_ky_khong_hop_le(int months) =>
        ReportPeriod.Create(months, Today).Error.ShouldBe(ReportingErrors.InvalidPeriod);

    [Theory]
    [InlineData(3)]
    [InlineData(12)]
    public void Ky_3_va_12_thang(int months) => ReportPeriod.Create(months, Today).Value.MonthStarts.Count.ShouldBe(months);

    [Fact]
    public void Ty_le_lam_tron_va_mau_so_0()
    {
        Ratio.Percent(1, 3).ShouldBe(33);
        Ratio.Percent(1, 8).ShouldBe(13); // 12,5 → 13 (0,5 làm tròn lên)
        Ratio.Percent(0, 5).ShouldBe(0);
        Ratio.Percent(3, 0).ShouldBeNull();
    }

    [Fact]
    public void BC01_tang_truong_va_E1_thang_truoc_bang_0()
    {
        Ratio.Growth(120_000_000, 100_000_000).ShouldBe(20.0m);
        Ratio.Growth(90, 70).ShouldBe(28.6m);
        Ratio.Growth(50, 100).ShouldBe(-50.0m);
        Ratio.Growth(10, 0).ShouldBeNull();
        Ratio.GrowthPercent(7, 6).ShouldBe(17);
        Ratio.GrowthPercent(7, 0).ShouldBeNull();
    }

    [Fact]
    public void BC05_E1_chuyen_can_chua_diem_danh_khong_co_so()
    {
        AttendanceTally.Empty.Rate.ShouldBeNull();
        (new AttendanceTally(17, 20) + new AttendanceTally(0, 0) + new AttendanceTally(1, 1)).ShouldBe(new AttendanceTally(18, 21));
        new AttendanceTally(18, 21).Rate.ShouldBe(86);
    }

    [Fact]
    public void BC05_cong_suat_phong_tren_81_gio()
    {
        RoomUtilization.OpenHoursPerWeek.ShouldBe(81m);
        RoomUtilization.Rate(0).ShouldBe(0);
        RoomUtilization.Rate(81 * 60).ShouldBe(100);
        RoomUtilization.Rate(27 * 60).ShouldBe(33);
        RoomUtilization.Hours(100).ShouldBe(1.7m);
    }

    [Theory]
    [InlineData(2026, 11, 23, 2026, 11, 23)] // thứ 2
    [InlineData(2026, 11, 24, 2026, 11, 23)] // thứ 3
    [InlineData(2026, 11, 29, 2026, 11, 23)] // chủ nhật
    public void BC05_tuan_tu_thu_2_den_chu_nhat(int y, int m, int d, int fy, int fm, int fd)
    {
        var (from, to) = RoomUtilization.WeekOf(new DateOnly(y, m, d));
        (from, to).ShouldBe((new DateOnly(fy, fm, fd), new DateOnly(fy, fm, fd).AddDays(6)));
    }

    [Fact]
    public void BC03_chien_dich_re_nhat_len_dau_chua_chot_xep_cuoi()
    {
        var a = new CampaignCost(Guid.NewGuid(), "FB-T10", "Facebook T10", 10_000_000, 40, 4);
        var b = new CampaignCost(Guid.NewGuid(), "TIKTOK", "TikTok", 3_000_000, 10, 2);
        var c = new CampaignCost(Guid.NewGuid(), "BANNER", "Banner", 2_000_000, 5, 0);
        var empty = new CampaignCost(Guid.NewGuid(), "NONE", "Chưa chạy", 0, 0, 0);

        CampaignCost.Rank([a, c, empty, b]).Select(x => x.Code).ShouldBe(["TIKTOK", "FB-T10", "BANNER"]);
        (a.CostPerLead, a.CostPerWon).ShouldBe((250_000m, 2_500_000m));
        (c.CostPerLead, c.CostPerWon).ShouldBe((400_000m, (decimal?)null));
    }
}
