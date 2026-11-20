using JPCenter.Modules.System.Application.Settings.Common;
using JPCenter.Modules.System.Domain.Settings;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Tests.Application;

public sealed class CenterSettingsJsonTests
{
    private static Dictionary<string, string> Rows(CenterSettings s) => CenterSettingsJson.ToRows(s).ToDictionary(r => r.Key, r => r.Value);

    [Fact]
    public void Ghi_json_giong_kieu_frontend()
    {
        var rows = Rows(CenterSettings.Default);
        rows.Count.ShouldBe(23);
        rows["crm.defaultSlaMinutes"].ShouldBe("15");
        rows["office.from"].ShouldBe("\"08:00\"");
        rows["invoice.codePattern"].ShouldBe("\"HD-{CN}-{YY}{MM}-{SO:4}\"");
        rows["invoice.resetEvery"].ShouldBe("\"month\"");
        rows["parent.weeklyReport"].ShouldBe("true");
        rows["safety.refundApprovalMin"].ShouldBe("0");
    }

    [Fact]
    public void Ghi_roi_doc_lai_ra_dung_bo_cu()
    {
        var d = CenterSettings.Default;
        var custom = d with
        {
            Office = d.Office with { SatTo = null, From = new(7, 45) },
            Invoice = new InvoiceSettings("{CN}{YYYY}-{SO:5}", InvoiceNumberReset.Year),
            Safety = new SafetySettings(Money.FromTrusted(2_000_000), 60),
            Parent = d.Parent with { ArrivalNotice = false, WeeklyTime = new(19, 30) },
        };
        var back = CenterSettingsJson.FromRows(Rows(custom), out var invalid);
        invalid.ShouldBeEmpty();
        back.ShouldBe(custom);
    }

    [Fact]
    public void Thieu_muc_lay_mac_dinh()
    {
        var back = CenterSettingsJson.FromRows(new Dictionary<string, string> { ["crm.defaultSlaMinutes"] = "30", ["academic.holidays"] = "[]" }, out var invalid);
        invalid.ShouldBeEmpty();
        back.ShouldBe(CenterSettings.Default with { Crm = new CrmSettings(30) });
    }

    [Fact]
    public void Muc_hong_lay_mac_dinh_va_bao_ten_muc()
    {
        var rows = new Dictionary<string, string>
        {
            ["crm.defaultSlaMinutes"] = "\"mười lăm\"", // sai kiểu
            ["office.to"] = "{không phải json",
            ["invoice.resetEvery"] = "\"week\"",
            ["safety.refundApprovalMin"] = "1500.5",     // tiền có số lẻ
        };
        var back = CenterSettingsJson.FromRows(rows, out var invalid);
        back.ShouldBe(CenterSettings.Default);
        invalid.ShouldBe(["office.to", "crm.defaultSlaMinutes", "invoice.resetEvery", "safety.refundApprovalMin"], ignoreOrder: true);
    }

    [Fact]
    public void Ca_bo_vi_pham_rang_buoc_thi_dung_mac_dinh()
    {
        var back = CenterSettingsJson.FromRows(new Dictionary<string, string> { ["office.from"] = "\"18:00\"" }, out var invalid);
        back.ShouldBe(CenterSettings.Default);
        invalid.Count.ShouldBe(23);
    }
}
