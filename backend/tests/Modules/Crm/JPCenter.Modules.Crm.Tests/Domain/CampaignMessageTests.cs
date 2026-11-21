using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Tests.Domain;

public sealed class CampaignMessageTests
{
    private static readonly BranchId Branch = new(Guid.NewGuid());
    private static readonly DateOnly Start = new(2026, 11, 1);
    private static readonly DateTimeOffset At = new(2026, 11, 21, 2, 0, 0, TimeSpan.Zero);

    private static CampaignDetails Details(DateOnly? end = null, int? target = 100, decimal spent = 3_000_000, string? utm = " FB-N5-T11 ",
        BranchId[]? branches = null) =>
        new(" fb-n5 ", "Facebook N5 tháng 11", LeadSource.Facebook, branches ?? [Branch], Start, end ?? Start.AddDays(30),
            Money.FromTrusted(10_000_000), Money.FromTrusted(spent), target, Guid.NewGuid(), utm, [], null);

    [Fact]
    public void CRM_16_tao_chien_dich_chuan_hoa_ma_va_utm()
    {
        var c = Campaign.Create(Details()).Value;
        (c.Code, c.Utm, c.Status).ShouldBe(("FB-N5", "fb-n5-t11", CampaignStatus.Planned));
        Campaign.NormalizeUtm("  ").ShouldBeNull();
    }

    [Fact]
    public void CRM_16_E1_ngay_ket_thuc_truoc_ngay_bat_dau_va_thieu_du_lieu()
    {
        Campaign.Create(Details(end: Start.AddDays(-1))).Error.ShouldBe(CampaignErrors.InvalidDates);
        Campaign.Create(Details(branches: [])).Error.ShouldBe(CampaignErrors.BranchRequired);
        Campaign.Create(Details(target: 0)).Error.ShouldBe(CampaignErrors.InvalidTarget);
        Campaign.Create(Details() with { Name = " " }).Error.ShouldBe(CampaignErrors.NameRequired);
        Campaign.Create(Details() with { Code = "" }).Error.ShouldBe(CampaignErrors.CodeRequired);
    }

    [Fact]
    public void CRM_16_chuyen_trang_thai()
    {
        var c = Campaign.Create(Details()).Value;
        c.Pause().Error.ShouldBe(CampaignErrors.InvalidStatus);
        c.Start().IsSuccess.ShouldBeTrue();
        c.Pause().IsSuccess.ShouldBeTrue();
        c.Start().IsSuccess.ShouldBeTrue();
        c.End().IsSuccess.ShouldBeTrue();
        c.Start().Error.ShouldBe(CampaignErrors.InvalidStatus);
        c.End().Error.ShouldBe(CampaignErrors.InvalidStatus);
    }

    [Fact]
    public void CRM_16_so_lieu_va_E3_chua_co_lead()
    {
        var c = Campaign.Create(Details()).Value;
        var s = CampaignStats.Of(c, 40, 6);
        (s.TargetPct, s.WinRate, s.CostPerLead, s.CostPerWin).ShouldBe((40m, 15m, Money.FromTrusted(75_000), Money.FromTrusted(500_000)));

        var empty = CampaignStats.Of(Campaign.Create(Details(target: null)).Value, 0, 0);
        (empty.TargetPct, empty.WinRate, empty.CostPerLead, empty.CostPerWin).ShouldBe(((decimal?)null, (decimal?)null, (Money?)null, (Money?)null));
    }

    [Fact]
    public void CRM_17_E1_mau_tin_can_ten_va_noi_dung()
    {
        MessageTemplate.Create(MessageScenario.Welcome, "Chào", ContactChannel.Zalo, " ").Error.ShouldBe(MessageErrors.BodyRequired);
        MessageTemplate.Create(MessageScenario.Welcome, " ", ContactChannel.Zalo, "x").Error.ShouldBe(MessageErrors.NameRequired);
        var t = MessageTemplate.Create(MessageScenario.Welcome, "Chào", ContactChannel.Zalo, "Chào {ten}!").Value;
        t.Active.ShouldBeTrue();
        t.SetActive(false);
        t.Active.ShouldBeFalse();
    }

    [Fact]
    public void CRM_17_E2_dien_bien_giu_nguyen_bien_thieu()
    {
        var t = MessageTemplate.Create(MessageScenario.TestReminder, "Nhắc test", ContactChannel.Zalo,
            "Chào {ten}, lịch test lúc {gio} tại {phong}. Gọi {hotline}.").Value;
        var text = t.Render(new Dictionary<string, string?> { ["ten"] = "Hoa", ["gio"] = "18:00", ["phong"] = "" });
        text.ShouldBe("Chào Hoa, lịch test lúc 18:00 tại {phong}. Gọi {hotline}.");
        MessageTemplateText.MissingVars(text).ShouldBe(["phong", "hotline"]);
    }

    [Fact]
    public void CRM_17_E4_E5_nhat_ky_tin_cho_gui_tin_tu_dong()
    {
        MessageLog.Create(ContactChannel.Sms, MessageScenario.Custom, null, "Hoa", " ", null, null, "x", null, At).Error
            .ShouldBe(MessageErrors.RecipientRequired);
        MessageLog.Create(ContactChannel.Sms, MessageScenario.Custom, null, "Hoa", "0912345678", null, null, " ", null, At).Error
            .ShouldBe(MessageErrors.MessageBodyRequired);

        var log = MessageLog.Create(ContactChannel.Zalo, MessageScenario.ConsultReminder, null, "Hoa", "0912345678", LeadId.New(), null,
            "Nhắc lịch", null, At).Value;
        (log.Auto, log.Status).ShouldBe((true, DeliveryStatus.Pending));
        log.MarkSent();
        log.Status.ShouldBe(DeliveryStatus.Sent);
        MessageLog.Create(ContactChannel.Zalo, MessageScenario.Custom, null, "Hoa", "0912345678", null, null, "Hi", Guid.NewGuid(), At)
            .Value.Auto.ShouldBeFalse();
    }
}
