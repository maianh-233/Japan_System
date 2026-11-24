using JPCenter.Modules.Reporting.Domain.TodayWork;

namespace JPCenter.Modules.Reporting.Tests.Domain;

/// <summary>User story 17 — BC-02 việc hôm nay theo vai trò (giờ Việt Nam)</summary>
public sealed class TodayWorkTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 14, 30, 0);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    [Theory]
    [InlineData(0, "0 phút")]
    [InlineData(59, "59 phút")]
    [InlineData(60, "1 giờ")]
    [InlineData(1439, "23 giờ")]
    [InlineData(1440, "1 ngày")]
    [InlineData(4000, "2 ngày")]
    [InlineData(-5, "0 phút")]
    public void Thoi_luong_doc_duoc(int minutes, string text) => HumanDuration.Of(minutes).ShouldBe(text);

    [Fact]
    public void BC02_lead_sla_con_nhieu_thoi_gian_khong_nhac()
    {
        // SLA 60 phút, mới 30 phút → còn 30 phút > 30% → chưa nhắc
        WorkRules.LeadSla(Now.AddMinutes(-30), 60, Now).ShouldBeNull();
    }

    [Fact]
    public void BC02_lead_sla_sap_qua_va_da_qua()
    {
        WorkRules.LeadSla(Now.AddMinutes(-45), 60, Now).ShouldBe(new WorkUrgency(WorkTone.Warning, "còn 15 phút"));
        WorkRules.LeadSla(Now.AddMinutes(-60), 60, Now).ShouldBe(new WorkUrgency(WorkTone.Danger, "quá 0 phút"));
        WorkRules.LeadSla(Now.AddHours(-3), 60, Now).ShouldBe(new WorkUrgency(WorkTone.Danger, "quá 2 giờ"));
    }

    [Fact]
    public void BC02_hen_goi_lai()
    {
        WorkRules.FollowUp(Now.AddDays(1), Now).ShouldBeNull();
        WorkRules.FollowUp(new DateTime(2026, 11, 22, 9, 0, 0), Now).ShouldBe(new WorkUrgency(WorkTone.Danger, "trễ hẹn 2 ngày"));
        WorkRules.FollowUp(new DateTime(2026, 11, 24, 9, 0, 0), Now).ShouldBe(new WorkUrgency(WorkTone.Warning, "hẹn 09:00"));
        WorkRules.FollowUp(new DateTime(2026, 11, 24, 16, 15, 0), Now).ShouldBe(new WorkUrgency(WorkTone.Info, "hẹn 16:15"));
    }

    [Fact]
    public void BC02_buoi_chua_diem_danh()
    {
        WorkRules.MissingAttendance(Today, new TimeOnly(18, 0), Now).ShouldBeNull(); // chưa bắt đầu
        WorkRules.MissingAttendance(Today, new TimeOnly(8, 0), Now).ShouldBe(new WorkUrgency(WorkTone.Warning, "hôm nay"));
        WorkRules.MissingAttendance(Today.AddDays(-3), new TimeOnly(18, 0), Now).ShouldBe(new WorkUrgency(WorkTone.Danger, "3 ngày trước"));
        WorkRules.MissingAttendance(Today.AddDays(-7), new TimeOnly(18, 0), Now).ShouldNotBeNull();
        WorkRules.MissingAttendance(Today.AddDays(-8), new TimeOnly(18, 0), Now).ShouldBeNull(); // quá 7 ngày
        WorkRules.MissingAttendance(Today.AddDays(1), new TimeOnly(8, 0), Now).ShouldBeNull();
    }

    [Fact]
    public void BC02_don_cho_duyet_buoi_hoc_duoi_24_gio_la_do()
    {
        var sent = new DateTime(2026, 11, 20, 10, 0, 0);
        WorkRules.PendingRequest(sent, Now.AddHours(5), Now).ShouldBe(new WorkUrgency(WorkTone.Danger, "buổi học < 24 giờ"));
        WorkRules.PendingRequest(sent, Now.AddDays(3), Now).ShouldBe(new WorkUrgency(WorkTone.Warning, "gửi 20/11"));
        WorkRules.PendingRequest(sent, null, Now).Tone.ShouldBe(WorkTone.Warning); // đơn Hr
    }

    [Fact]
    public void BC02_hoa_don_den_han_va_qua_han()
    {
        WorkRules.InvoiceDue(Today.AddDays(4), Today).ShouldBeNull();
        WorkRules.InvoiceDue(Today.AddDays(3), Today).ShouldBe(new WorkUrgency(WorkTone.Info, "hạn 27/11"));
        WorkRules.InvoiceDue(Today, Today).ShouldBe(new WorkUrgency(WorkTone.Warning, "hạn hôm nay"));
        WorkRules.InvoiceDue(Today.AddDays(-5), Today).ShouldBe(new WorkUrgency(WorkTone.Danger, "quá hạn 5 ngày"));
    }

    [Fact]
    public void BC02_giao_dich_chua_khop()
    {
        WorkRules.UnmatchedTransaction(Now.AddHours(-2), Now).ShouldBe(new WorkUrgency(WorkTone.Warning, "12:30"));
        WorkRules.UnmatchedTransaction(Now.AddDays(-1).AddHours(-1), Now).ShouldBe(new WorkUrgency(WorkTone.Warning, "1 ngày trước"));
        WorkRules.UnmatchedTransaction(Now.AddDays(-2), Now).ShouldBe(new WorkUrgency(WorkTone.Danger, "2 ngày trước"));
    }

    [Fact]
    public void BC02_xep_do_vang_xanh_cung_muc_cu_truoc()
    {
        WorkItem Item(string key, WorkTone tone, int minutesAgo) => new(key, key, null, new WorkUrgency(tone, ""), "/", Now.AddMinutes(-minutesAgo));

        var ordered = WorkItem.Order([Item("info", WorkTone.Info, 100), Item("warn-new", WorkTone.Warning, 5), Item("danger", WorkTone.Danger, 1),
            Item("warn-old", WorkTone.Warning, 50)]);
        ordered.Select(i => i.Key).ShouldBe(["danger", "warn-old", "warn-new", "info"]);
    }
}
