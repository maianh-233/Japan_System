using JPCenter.Modules.Reporting.Domain.Alerts;
using JPCenter.Modules.Reporting.Domain.Branches;
using JPCenter.Modules.Reporting.Domain.Metrics;
using JPCenter.Modules.Reporting.Domain.TodayWork;

namespace JPCenter.Modules.Reporting.Tests.Domain;

/// <summary>User story 17 — BC-01 cảnh báo bảng điều khiển, BC-06 so sánh chi nhánh</summary>
public sealed class DashboardTests
{
    private static BranchFigures Branch(int attended = 90, int total = 100, decimal overdue = 0, int seats = 40, int leads = 10, int won = 3) =>
        new(Guid.NewGuid(), "Cầu Giấy", leads, won, 30, 3, seats, 26, new AttendanceTally(attended, total), 120_000_000, 20_000_000, 15_000_000, overdue);

    [Fact]
    public void BC06_ty_le_chi_nhanh()
    {
        var b = BranchComparison.From(Branch());
        (b.Conversion, b.Fill, b.Attendance, b.NeedsAttention).ShouldBe((30, 65, 90, false));
    }

    [Fact]
    public void BC06_E1_chuyen_can_duoi_85_hoac_no_qua_han_can_chu_y()
    {
        var low = BranchComparison.From(Branch(attended: 84));
        (low.LowAttendance, low.HasOverdue, low.NeedsAttention).ShouldBe((true, false, true));

        var debt = BranchComparison.From(Branch(overdue: 1_500_000));
        (debt.LowAttendance, debt.HasOverdue, debt.NeedsAttention).ShouldBe((false, true, true));

        BranchComparison.From(Branch(attended: 85)).NeedsAttention.ShouldBeFalse();
    }

    [Fact]
    public void BC06_chi_nhanh_moi_chua_co_so_lieu_khong_bi_danh_dau()
    {
        var b = BranchComparison.From(Branch(attended: 0, total: 0, seats: 0, leads: 0, won: 0));
        (b.Conversion, b.Fill, b.Attendance, b.NeedsAttention).ShouldBe(((int?)null, (int?)null, (int?)null, false));
    }

    [Fact]
    public void BC01_canh_bao_chi_tao_khi_co_viec()
    {
        DashboardAlert.LeadSlaOverdue(0).ShouldBeNull();
        DashboardAlert.UnconfirmedSessions(0).ShouldBeNull();
        DashboardAlert.MissingTeacherLogs(0).ShouldBeNull();
        DashboardAlert.LowFillClasses([]).ShouldBeNull();

        DashboardAlert.LeadSlaOverdue(3).ShouldBe(new DashboardAlert(DashboardAlertKind.LeadSlaOverdue, WorkTone.Danger,
            "3 lead mới quá hạn liên hệ (SLA)", "/admin/crm/lead-rules"));
        DashboardAlert.UnconfirmedSessions(2)!.Text.ShouldBe("2 buổi đã dạy chưa xác nhận (ảnh hưởng lương GV)");
        DashboardAlert.MissingTeacherLogs(1)!.Tone.ShouldBe(WorkTone.Warning);
    }

    [Fact]
    public void BC01_lop_vang_neu_toi_da_3_ma_lop()
    {
        var a = DashboardAlert.LowFillClasses(["N5-K12", "N4-K03", "N3-K01", "N5-K14"])!;
        a.Text.ShouldBe("4 lớp lấp đầy dưới 50%: N5-K12, N4-K03, N3-K01");
        DashboardAlert.IsLowFill(49).ShouldBeTrue();
        DashboardAlert.IsLowFill(50).ShouldBeFalse();
        DashboardAlert.IsLowFill(null).ShouldBeFalse();
    }

    [Fact]
    public void BC01_tich_hop_loi_chi_lay_phan_truoc_gach_dai()
    {
        DashboardAlert.IntegrationError("Zalo OA", "Token hết hạn — kết nối lại trong Cài đặt").Text.ShouldBe("Tích hợp Zalo OA lỗi: Token hết hạn");
        DashboardAlert.IntegrationError("SMS", null).Text.ShouldBe("Tích hợp SMS lỗi");
    }
}
