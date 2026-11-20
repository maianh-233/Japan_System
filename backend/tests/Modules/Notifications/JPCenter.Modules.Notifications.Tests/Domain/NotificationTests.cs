using JPCenter.Modules.Notifications.Domain.Notifications;

namespace JPCenter.Modules.Notifications.Tests.Domain;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    private static Notification Create(string title = "Đơn hủy buổi mới", string? description = "GV Lan xin hủy buổi 12", string? link = "/admin/workflow/requests") =>
        Notification.Create(RecipientType.Staff, Guid.NewGuid(), title, description, NotificationTone.Warning, link, "academic.cancel_request.created", At).Value;

    [Fact]
    public void Tao_thong_bao_chua_doc_va_cat_khoang_trang()
    {
        var n = Notification.Create(RecipientType.Portal, Guid.NewGuid(), "  Đơn nghỉ đã duyệt ", null, NotificationTone.Success, " ", null, At).Value;
        n.Title.ShouldBe("Đơn nghỉ đã duyệt");
        n.Description.ShouldBe(string.Empty);
        n.Link.ShouldBeNull();
        n.IsRead.ShouldBeFalse();
        n.CreatedAt.ShouldBe(At);
    }

    [Fact]
    public void TB_04_E2_thieu_tieu_de_hoac_nguoi_nhan_bi_chan()
    {
        Notification.Create(RecipientType.Staff, Guid.NewGuid(), " ", "x", NotificationTone.Info, null, null, At).Error.ShouldBe(NotificationErrors.TitleRequired);
        Notification.Create(RecipientType.Staff, Guid.Empty, "x", "x", NotificationTone.Info, null, null, At).Error.ShouldBe(NotificationErrors.RecipientRequired);
    }

    [Theory]
    [InlineData("https://evil.example/login")]
    [InlineData("//evil.example")]
    [InlineData("admin/crm")]
    public void Link_chi_nhan_duong_dan_noi_bo(string link) =>
        Notification.Create(RecipientType.Staff, Guid.NewGuid(), "x", null, NotificationTone.Info, link, null, At).Error.ShouldBe(NotificationErrors.LinkInvalid);

    [Fact]
    public void Noi_dung_qua_dai_bi_cat_khong_chan()
    {
        var n = Create(title: new string('a', 300), description: new string('b', 2000));
        n.Title.Length.ShouldBe(Notification.TitleMaxLength);
        n.Title.ShouldEndWith("…");
        n.Description.Length.ShouldBe(Notification.DescriptionMaxLength);
    }

    [Fact]
    public void TB_02_doc_lai_khong_doi_thoi_diem_doc()
    {
        var n = Create();
        n.MarkRead(At.AddMinutes(5)).ShouldBeTrue();
        n.MarkRead(At.AddMinutes(9)).ShouldBeFalse();
        n.ReadAt.ShouldBe(At.AddMinutes(5));
        n.IsRead.ShouldBeTrue();
    }

    [Fact]
    public void TB_05_don_thong_bao_da_doc_qua_90_ngay_hoac_moi_thong_bao_qua_180_ngay()
    {
        var unread = Create();
        unread.IsStale(At.AddDays(179), readDays: 90, maxDays: 180).ShouldBeFalse();
        unread.IsStale(At.AddDays(180), readDays: 90, maxDays: 180).ShouldBeTrue();

        var read = Create();
        read.MarkRead(At.AddDays(1));
        read.IsStale(At.AddDays(90), readDays: 90, maxDays: 180).ShouldBeFalse();
        read.IsStale(At.AddDays(91), readDays: 90, maxDays: 180).ShouldBeTrue();
    }
}
