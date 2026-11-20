using JPCenter.Modules.System.Domain.Settings;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Tests.Domain;

public sealed class CenterSettingsTests
{
    private static readonly CenterSettings D = CenterSettings.Default;

    [Fact]
    public void Mac_dinh_hop_le_va_du_23_muc_co_nhan() // khớp DEFAULT_SETTINGS + SETTING_LABELS của frontend
    {
        D.Validate().IsSuccess.ShouldBeTrue();
        var entries = D.ToEntries();
        entries.Select(e => e.Key).ShouldBe(CenterSettings.Labels.Keys);
        entries.Single(e => e.Key == "invoice.codePattern").Value.ShouldBe("HD-{CN}-{YY}{MM}-{SO:4}");
        entries.Single(e => e.Key == "invoice.resetEvery").Value.ShouldBe("month");
        entries.Single(e => e.Key == "office.to").Value.ShouldBe("17:30");
        entries.Single(e => e.Key == "parent.arrivalNotice").Value.ShouldBe(true);
        entries.Single(e => e.Key == "safety.refundApprovalMin").Value.ShouldBe(0L);
    }

    [Fact]
    public void CD_02_E1_sai_khoang_tra_loi_theo_tung_muc()
    {
        var bad = D with
        {
            Crm = new CrmSettings(0),
            AtRisk = D.AtRisk with { Streak = 21, AttendanceRate = 101 },
            Safety = D.Safety with { TrashDays = 0 },
        };
        var error = bad.Validate().Error;
        error.Code.ShouldBe("validation.failed");
        error.FieldErrors!.Keys.ShouldBe(["crm.defaultSlaMinutes", "atRisk.streak", "atRisk.attendanceRate", "safety.trashDays"], ignoreOrder: true);
    }

    [Fact]
    public void CD_02_E2_gio_hanh_chinh_ket_thuc_phai_sau_bat_dau()
    {
        (D with { Office = D.Office with { From = new(18, 0) } }).Validate().Error.ShouldBe(SettingsErrors.OfficeHoursInvalid);
        (D with { Office = D.Office with { SatTo = new(7, 0) } }).Validate().Error.ShouldBe(SettingsErrors.OfficeHoursInvalid);
        (D with { Office = D.Office with { SatTo = null } }).Validate().IsSuccess.ShouldBeTrue(); // nghỉ thứ 7
    }

    [Theory]
    [InlineData("HD-{CN}-{YY}{MM}-{SO:4}", true)]
    [InlineData("{CN}{YYYY}{SO}", true)]
    [InlineData("HD-{CN}-{YY}{MM}", false)]          // thiếu số thứ tự
    [InlineData("HD-{SO}-{SO:3}", false)]            // 2 biến số thứ tự
    [InlineData("HD-{CN}-{DD}-{SO}", false)]         // biến lạ
    [InlineData("HD-{SO:0}", false)]                 // n phải 1 → 9
    [InlineData("HD-{CN-{SO}", false)]               // ngoặc lẻ
    [InlineData("HD-{SO}}", false)]
    [InlineData("HOADON-TRUNG-TAM-NHAT-NGU-{CN}-{YYYY}-{SO:6}", false)] // > 40 ký tự
    public void CD_02_E3_mau_ma_hoa_don(string pattern, bool ok)
    {
        var result = (D with { Invoice = D.Invoice with { CodePattern = pattern } }).Validate();
        if (ok) result.IsSuccess.ShouldBeTrue();
        else result.Error.ShouldBe(SettingsErrors.InvoicePatternInvalid);
    }

    [Fact]
    public void CD_02_E4_muc_trung_binh_phai_nho_hon_muc_cao() =>
        (D with { AtRisk = D.AtRisk with { MediumFrom = 5, HighFrom = 5 } }).Validate().Error.ShouldBe(SettingsErrors.AtRiskLevelsInvalid);

    [Fact]
    public void Kieu_danh_so_lai_la_phai_bao_loi_theo_muc() =>
        (D with { Invoice = D.Invoice with { ResetEvery = (InvoiceNumberReset)9 } }).Validate().Error.FieldErrors!.Keys.ShouldBe(["invoice.resetEvery"]);

    [Fact]
    public void So_sanh_chi_tra_muc_doi_voi_nhan_va_gia_tri_hien_thi()
    {
        var next = D with
        {
            Parent = D.Parent with { WeeklyReport = false, WeeklyDay = 6 },
            Office = D.Office with { SatTo = null },
            Safety = D.Safety with { RefundApprovalMin = Money.FromTrusted(500_000) },
        };
        var changes = D.Diff(next);
        changes.Select(c => (c.Field, c.Before, c.After)).ShouldBe(
        [
            ("Thứ 7 làm đến", "12:00", "Nghỉ"),
            ("Tự gửi báo cáo tuần", "Bật", "Tắt"),
            ("Ngày gửi báo cáo tuần", "Chủ nhật", "Thứ 7"),
            ("Hoàn tiền cần duyệt từ (đ)", "0đ", Money.FromTrusted(500_000).ToString()),
        ]);
    }

    [Fact]
    public void CD_02_E5_khong_doi_gi_thi_khong_co_thay_doi() => D.Diff(D with { }).ShouldBeEmpty();

    [Fact]
    public void Muc_setting_bao_co_doi_hay_khong()
    {
        var setting = Setting.Create("crm.defaultSlaMinutes", "15");
        setting.Change("15").ShouldBeFalse();
        setting.Change("30").ShouldBeTrue();
        setting.Value.ShouldBe("30");
    }
}
