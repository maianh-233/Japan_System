using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Tests.Domain;

public sealed class IntegrationTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 11, 24, 3, 0, 0, TimeSpan.Zero);

    private static Integration New(string key) => Integration.Create(key, key, "").Value;

    private static Integration Connected(string key, Dictionary<string, string?> config, params string[] events)
    {
        var i = New(key);
        i.Connect(config, "acc", Admin, At).IsSuccess.ShouldBeTrue();
        foreach (var e in events) i.SetEvent(e, true).IsSuccess.ShouldBeTrue();
        return i;
    }

    private static Integration Zalo(params string[] events) =>
        Connected(IntegrationKeys.ZaloOa, new() { ["oaId"] = "1", ["appId"] = "2", ["secret"] = "s3cr3tKey" }, events);

    private static Integration Sms(params string[] events) =>
        Connected(IntegrationKeys.Sms, new() { ["brandname"] = "JPCENTER", ["apiKey"] = "sk_live_1234" }, events);

    [Fact]
    public void TH_02_E3_khoa_ngoai_danh_muc()
    {
        Integration.Create("slack", "Slack", "").Error.ShouldBe(IntegrationErrors.Unknown);
        var z = New(IntegrationKeys.Zoom);
        z.Category.ShouldBe(IntegrationCategory.Meeting);
        z.Status.ShouldBe(IntegrationStatus.Disconnected);
        z.Events.Keys.ShouldBe(["autoRoom", "record"], ignoreOrder: true);
    }

    [Fact]
    public void TH_02_E1_E4_thieu_truong_bat_buoc_bo_truong_la()
    {
        var z = New(IntegrationKeys.ZaloOa);
        var err = z.Connect(new Dictionary<string, string?> { ["oaId"] = "1", ["appId"] = " " }, null, Admin, At).Error;
        err.Code.ShouldBe("system.integration.missing_field");
        err.FieldErrors!.Keys.ShouldBe(["appId"]);
        z.Status.ShouldBe(IntegrationStatus.Disconnected);

        z.Connect(new Dictionary<string, string?> { ["oaId"] = " 1 ", ["appId"] = "2", ["secret"] = "x", ["hack"] = "y" }, " OA ", Admin, At)
            .IsSuccess.ShouldBeTrue();
        z.Config.ShouldBe(new Dictionary<string, string> { ["oaId"] = "1", ["appId"] = "2", ["secret"] = "x" });
        z.Account.ShouldBe("OA");
        z.Status.ShouldBe(IntegrationStatus.Connected);
    }

    [Fact]
    public void TH_02_E2_ket_noi_lai_de_trong_bi_mat_giu_khoa_cu()
    {
        var z = Zalo();
        z.MarkError("401");
        z.Connect(new Dictionary<string, string?> { ["oaId"] = "9", ["appId"] = "2", ["secret"] = "" }, null, Admin, At).IsSuccess.ShouldBeTrue();
        z.Config["secret"].ShouldBe("s3cr3tKey");
        z.Config["oaId"].ShouldBe("9");
        z.LastError.ShouldBeNull();
        z.Status.ShouldBe(IntegrationStatus.Connected);
    }

    [Fact]
    public void TH_01_E2_che_bi_mat_chi_4_ky_tu_cuoi()
    {
        var masked = Zalo().MaskedConfig(v => v);
        masked["secret"].ShouldBe("••••••tKey");
        masked["oaId"].ShouldBe("1");
        Integration.Mask("abc").ShouldBe("••••••");
    }

    [Fact]
    public void Ma_hoa_lai_chi_truong_bi_mat_khong_doi_thi_bao_khong_thay_doi()
    {
        var z = Zalo();
        z.RewriteSecrets(v => v.StartsWith("enc:") ? v : "enc:" + v).ShouldBeTrue();
        z.Config["secret"].ShouldBe("enc:s3cr3tKey");
        z.Config["oaId"].ShouldBe("1");
        z.RewriteSecrets(v => v.StartsWith("enc:") ? v : "enc:" + v).ShouldBeFalse();
    }

    [Fact]
    public void TH_02_ngat_xoa_cau_hinh_tat_viec()
    {
        var z = Zalo("tuition", "attendance");
        z.Disconnect();
        z.Status.ShouldBe(IntegrationStatus.Disconnected);
        z.Config.ShouldBeEmpty();
        z.Events.Values.ShouldAllBe(on => !on);
    }

    [Fact]
    public void TH_03_bat_viec_chi_khi_da_ket_noi()
    {
        var m = New(IntegrationKeys.Meet);
        m.SetEvent("autoRoom", true).Error.ShouldBe(IntegrationErrors.NotConnected);
        m.SetEvent("autoRoom", false).IsSuccess.ShouldBeTrue();
        m.SetEvent("record", true).Error.ShouldBe(IntegrationErrors.UnknownEvent);
        var z = Zalo();
        z.MarkError("401");
        z.SetEvent("tuition", true).IsSuccess.ShouldBeTrue();
        z.IsActive("tuition").ShouldBeFalse(); // đang lỗi thì không dùng được
    }

    [Fact]
    public void TH_04_ghi_luot_goi_loi_xac_thuc_chuyen_error_thanh_cong_het_loi()
    {
        var z = Zalo("tuition");
        var log = z.RecordCall(false, "timeout", At);
        log.Ok.ShouldBeFalse();
        z.Status.ShouldBe(IntegrationStatus.Connected); // lỗi tạm chỉ ghi nhật ký

        z.RecordCall(false, "401 Unauthorized", At, authFailure: true);
        z.Status.ShouldBe(IntegrationStatus.Error);
        z.LastError.ShouldBe("401 Unauthorized");

        z.RecordCall(true, "OK", At.AddMinutes(5));
        z.Status.ShouldBe(IntegrationStatus.Connected);
        z.LastError.ShouldBeNull();
        z.LastSyncAt.ShouldBe(At.AddMinutes(5));

        IntegrationLog.Create(z.Id, true, new string('x', 2000), At).Message.Length.ShouldBe(IntegrationLog.MaxMessage);
    }

    [Fact]
    public void TH_06_Zalo_truoc_SMS_du_phong()
    {
        ChannelPlan.For("tuition", [Zalo("tuition"), Sms("fallback")]).ShouldBe([MessageChannel.Zalo, MessageChannel.Sms]);
        ChannelPlan.For("tuition", [Zalo("tuition"), Sms()]).ShouldBe([MessageChannel.Zalo]);
    }

    [Fact]
    public void TH_06_Zalo_khong_chay_thi_gui_SMS_admin_tat_viec_thi_khong_gui()
    {
        var broken = Zalo("tuition");
        broken.MarkError("401");
        ChannelPlan.For("tuition", [broken, Sms("fallback")]).ShouldBe([MessageChannel.Sms]);
        ChannelPlan.For("tuition", [New(IntegrationKeys.ZaloOa), Sms("fallback")]).ShouldBe([MessageChannel.Sms]);
        ChannelPlan.For("tuition", [Zalo("attendance"), Sms("fallback")]).ShouldBeEmpty();
        ChannelPlan.For("tuition", []).ShouldBeEmpty();
    }

    [Fact]
    public void TH_06_OTP_chi_SMS_email_khong_du_phong_cheo()
    {
        ChannelPlan.For(ChannelPlan.Otp, [Zalo("tuition"), Sms("otp")]).ShouldBe([MessageChannel.Sms]);
        ChannelPlan.For(ChannelPlan.Otp, [Sms("fallback")]).ShouldBeEmpty();
        var email = Connected(IntegrationKeys.Email,
            new() { ["host"] = "smtp", ["port"] = "587", ["user"] = "u", ["pass"] = "p" }, "invoice");
        ChannelPlan.For("invoice", [email, Sms("fallback")]).ShouldBe([MessageChannel.Email]);
        ChannelPlan.For("invite", [email, Sms("fallback")]).ShouldBeEmpty();
    }
}
