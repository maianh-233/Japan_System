using JPCenter.Application.Common.Email;
using JPCenter.Infrastructure.Common.Email;
using Microsoft.Extensions.Configuration;

namespace JPCenter.IntegrationTests.BuildingBlocks;

/// <summary>Khuôn email dùng chung (BuildingBlocks): an toàn HTML, đủ thông tin thương hiệu, có bản chữ thuần.</summary>
public sealed class EmailRendererTests
{
    private static readonly EmailOptions Options = EmailOptions.Load(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["App:CenterName"] = "JP Center",
        ["App:Hotline"] = "1900 0000",
        ["App:SupportEmail"] = "hotro@jpcenter.vn",
    }).Build());

    private static readonly EmailRenderer Renderer = new(Options);

    private static EmailContent Sample(string name) =>
        EmailContent.Create("Tiêu đề", "Tiêu đề lớn", "Xem trước")
            .Hello(name)
            .Text("Đoạn có **chữ đậm** ở giữa.")
            .Code("123456", "Mã")
            .Button("Đăng nhập", "http://localhost:5173/login?x=1&y=2")
            .Details(("Thiết bị", "Chrome · Windows"))
            .Items(["Mục 1"])
            .Notice("Cẩn thận", EmailTone.Danger)
            .Footnote("Ghi chú nhỏ");

    [Fact]
    public void Gia_tri_nguoi_dung_duoc_ma_hoa_HTML()
    {
        var html = Renderer.Html(Sample("<script>alert(1)</script>"));
        html.ShouldNotContain("<script>alert(1)</script>");
        html.ShouldContain("&lt;script&gt;");
        html.ShouldContain("login?x=1&amp;y=2");
    }

    [Fact]
    public void Chu_dam_thanh_strong_va_co_du_thuong_hieu()
    {
        var html = Renderer.Html(Sample("An"));
        html.ShouldContain(">chữ đậm</strong>");
        html.ShouldNotContain("**");
        html.ShouldContain("1900 0000");
        html.ShouldContain("mailto:hotro@jpcenter.vn");
        html.ShouldContain("Xem trước"); // preheader
        html.ShouldContain("lang=\"vi\"");
    }

    [Fact]
    public void Ban_chu_thuan_co_ma_link_va_chan_trang()
    {
        var text = Renderer.Text(Sample("An"));
        text.ShouldContain("Xin chào An,");
        text.ShouldContain("Mã: 123456");
        text.ShouldContain("Đăng nhập: http://localhost:5173/login?x=1&y=2");
        text.ShouldContain("Đoạn có chữ đậm ở giữa.");
        text.ShouldContain("Hotline: 1900 0000");
        text.ShouldNotContain("**");
    }
}
