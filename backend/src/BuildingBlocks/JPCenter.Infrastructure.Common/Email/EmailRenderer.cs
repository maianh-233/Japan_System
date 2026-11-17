using System.Net;
using System.Text;
using JPCenter.Application.Common.Email;

namespace JPCenter.Infrastructure.Common.Email;

/// <summary>
/// Dựng HTML email theo khuôn thương hiệu chung: thanh logo · thẻ nội dung · chân trang (hotline, email hỗ trợ, địa chỉ).
/// Viết theo chuẩn email (bảng + CSS inline, rộng tối đa 600px) để hiển thị đúng trên Gmail, Outlook, điện thoại.
/// Mọi giá trị đều được mã hóa HTML.
/// </summary>
public sealed class EmailRenderer(EmailOptions options)
{
    // Màu trùng frontend (assets/styles/index.css)
    private const string Primary = "#1f6fc5";
    private const string PrimaryDark = "#1a5fad";
    private const string PrimarySoft = "#f3f8fe";
    private const string Page = "#f5f9ff";
    private const string Line = "#e2e8f0";
    private const string Ink = "#1e293b";
    private const string InkSecondary = "#475569";
    private const string InkMuted = "#64748b";
    /// <summary>Màn hình hẹp (điện thoại): thu lề — Gmail / Apple Mail hiểu media query trong &lt;style&gt;</summary>
    private const string MobileCss =
        "@media only screen and (max-width:480px){.card{padding:24px 20px 20px !important;border-radius:12px !important;}.outer{padding:20px 12px !important;}}";
    private const string Font = "'Be Vietnam Pro','Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Mã hóa HTML rồi đổi **đậm** thành &lt;strong&gt;</summary>
    private static string Rich(string text) => EmailContent.BoldRegex().Replace(E(text), $"<strong style=\"color:{Ink}\">$1</strong>");

    public string Html(EmailContent content)
    {
        var body = new StringBuilder();
        if (content.Greeting is not null) body.Append(P(E(content.Greeting)));
        foreach (var block in content.Blocks) body.Append(Block(block));

        return $"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="light">
            <meta name="supported-color-schemes" content="light">
            <title>{E(content.Subject)}</title>
            <style>{MobileCss}</style>
            </head>
            <body style="margin:0;padding:0;background:{Page};-webkit-text-size-adjust:100%;">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">{E(content.Preheader)}&#8199;&#65279;&#847;&#8199;&#65279;&#847;</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{Page};">
            <tr><td class="outer" align="center" style="padding:32px 16px;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;">
                <tr><td style="padding:0 4px 20px;">
                  <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
                    <td width="40" height="40" align="center" valign="middle" style="width:40px;height:40px;background:{Primary};border-radius:10px;color:#ffffff;font-family:'Noto Sans JP',{Font};font-size:20px;font-weight:700;">日</td>
                    <td style="padding-left:12px;font-family:{Font};">
                      <div style="font-size:17px;font-weight:700;color:{Ink};line-height:22px;">{E(options.CenterName)}</div>
                      <div style="font-size:12px;color:{InkMuted};line-height:18px;">{E(options.Tagline)}</div>
                    </td>
                  </tr></table>
                </td></tr>
                <tr><td class="card" style="background:#ffffff;border:1px solid {Line};border-radius:14px;padding:32px 32px 28px;font-family:{Font};">
                  <h1 style="margin:0 0 20px;font-size:21px;line-height:30px;font-weight:700;color:{Ink};">{E(content.Heading)}</h1>
                  {body}
                </td></tr>
                <tr><td style="padding:24px 8px 0;font-family:{Font};font-size:12px;line-height:19px;color:{InkMuted};text-align:center;">
                  {Footer()}
                </td></tr>
              </table>
            </td></tr>
            </table>
            </body>
            </html>
            """;
    }

    /// <summary>Bản chữ thuần kèm chân trang</summary>
    public string Text(EmailContent content)
    {
        var sb = new StringBuilder(content.ToPlainText()).AppendLine().AppendLine().AppendLine("—").AppendLine(options.CenterName);
        if (options.Hotline is not null) sb.AppendLine($"Hotline: {options.Hotline}");
        if (options.SupportEmail is not null) sb.AppendLine($"Hỗ trợ: {options.SupportEmail}");
        if (options.Address is not null) sb.AppendLine(options.Address);
        sb.AppendLine("Email gửi tự động từ hệ thống, vui lòng không trả lời trực tiếp.");
        return sb.ToString();
    }

    private string Footer()
    {
        var contact = new List<string>();
        if (options.Hotline is not null) contact.Add($"Hotline <a href=\"tel:{E(options.Hotline.Replace(" ", ""))}\" style=\"color:{InkSecondary};text-decoration:none;\">{E(options.Hotline)}</a>");
        if (options.SupportEmail is not null) contact.Add($"<a href=\"mailto:{E(options.SupportEmail)}\" style=\"color:{InkSecondary};text-decoration:none;\">{E(options.SupportEmail)}</a>");
        return $"""
            <div style="font-weight:600;color:{InkSecondary};">{E(options.CenterName)}</div>
            {(options.Address is null ? "" : $"<div>{E(options.Address)}</div>")}
            {(contact.Count == 0 ? "" : $"<div>{string.Join(" &nbsp;·&nbsp; ", contact)}</div>")}
            <div style="margin-top:10px;">Email gửi tự động từ hệ thống quản lý {E(options.CenterName)}, vui lòng không trả lời trực tiếp.<br>
            Cần hỗ trợ? Liên hệ hotline hoặc email bên trên.</div>
            <div style="margin-top:10px;">© {DateTime.UtcNow.Year} {E(options.CenterName)}</div>
            """;
    }

    private static string P(string html, string color = InkSecondary, int size = 15) =>
        $"<p style=\"margin:0 0 16px;font-size:{size}px;line-height:24px;color:{color};\">{html}</p>";

    private static string Block(EmailBlock block) => block switch
    {
        EmailBlock.Paragraph p => P(Rich(p.Text)),
        EmailBlock.CodeBox c => $"""
            {(c.Caption is null ? "" : $"<p style=\"margin:0 0 8px;font-size:13px;color:{InkMuted};\">{E(c.Caption)}</p>")}
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 20px;"><tr>
              <td style="background:{PrimarySoft};border:1px dashed {Primary};border-radius:10px;padding:14px 26px;font-family:Consolas,'Courier New',monospace;font-size:30px;font-weight:700;letter-spacing:8px;color:{PrimaryDark};">{E(c.Code)}</td>
            </tr></table>
            """,
        EmailBlock.Action a => $"""
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:8px 0 20px;"><tr>
              <td align="center" bgcolor="{Primary}" style="border-radius:10px;">
                <a href="{E(a.Url)}" target="_blank" style="display:inline-block;padding:13px 28px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:10px;">{E(a.Label)}</a>
              </td>
            </tr></table>
            <p style="margin:0 0 16px;font-size:12px;line-height:18px;color:{InkMuted};">Nút không bấm được? Mở đường dẫn sau:<br><a href="{E(a.Url)}" style="color:{Primary};word-break:break-all;">{E(a.Url)}</a></p>
            """,
        EmailBlock.Table t => $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 20px;border:1px solid {Line};border-radius:10px;border-collapse:separate;">
            {string.Join("", t.Rows.Select((r, i) => $"<tr><td style=\"padding:10px 14px;{(i > 0 ? $"border-top:1px solid {Line};" : "")}font-size:13px;color:{InkMuted};width:38%;\">{E(r.Label)}</td><td style=\"padding:10px 14px;{(i > 0 ? $"border-top:1px solid {Line};" : "")}font-size:14px;color:{Ink};font-weight:600;\">{E(r.Value)}</td></tr>"))}
            </table>
            """,
        EmailBlock.List l => $"""
            <ul style="margin:0 0 16px;padding-left:20px;font-size:15px;line-height:24px;color:{InkSecondary};">
            {string.Join("", l.Items.Select(i => $"<li style=\"margin:0 0 6px;\">{Rich(i)}</li>"))}
            </ul>
            """,
        EmailBlock.Callout n => Callout(n),
        EmailBlock.Small s => $"<p style=\"margin:20px 0 0;padding-top:16px;border-top:1px solid {Line};font-size:13px;line-height:20px;color:{InkMuted};\">{Rich(s.Text)}</p>",
        _ => "",
    };

    private static string Callout(EmailBlock.Callout n)
    {
        var (bg, border, color) = n.Tone switch
        {
            EmailTone.Danger => ("#fef2f2", "#dc2626", "#991b1b"),
            EmailTone.Warning => ("#fff8eb", "#d97706", "#92400e"),
            _ => (PrimarySoft, Primary, PrimaryDark),
        };
        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 20px;"><tr>
              <td style="background:{bg};border-left:4px solid {border};border-radius:8px;padding:12px 16px;font-size:14px;line-height:22px;color:{color};">{Rich(n.Text)}</td>
            </tr></table>
            """;
    }
}
