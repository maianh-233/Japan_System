using System.Text;
using System.Text.RegularExpressions;

namespace JPCenter.Application.Common.Email;

/// <summary>
/// Nội dung 1 email (chỉ NỘI DUNG — khung thương hiệu, màu, chân trang do <see cref="IEmailSender"/> áp khi gửi).
/// Tạo bằng <see cref="Create"/> rồi nối các khối: đoạn văn, mã, nút, bảng thông tin, danh sách, lưu ý.
/// Trong đoạn văn dùng <c>**chữ đậm**</c>; mọi giá trị đều được mã hóa HTML khi dựng.
/// </summary>
public sealed partial class EmailContent
{
    private readonly List<EmailBlock> _blocks = [];

    private EmailContent(string subject, string heading, string preheader) => (Subject, Heading, Preheader) = (subject, heading, preheader);

    /// <summary>Tiêu đề email (hộp thư)</summary>
    public string Subject { get; }
    /// <summary>Tiêu đề lớn trong thân email</summary>
    public string Heading { get; }
    /// <summary>Dòng xem trước hiện cạnh tiêu đề trong hộp thư (không hiện trong thân)</summary>
    public string Preheader { get; }
    public string? Greeting { get; private set; }
    public IReadOnlyList<EmailBlock> Blocks => _blocks;

    public static EmailContent Create(string subject, string heading, string preheader) => new(subject, heading, preheader);

    /// <summary>"Xin chào {name},"</summary>
    public EmailContent Hello(string? name)
    {
        Greeting = string.IsNullOrWhiteSpace(name) ? "Xin chào," : $"Xin chào {name.Trim()},";
        return this;
    }

    public EmailContent Text(string text) => Add(new EmailBlock.Paragraph(text));

    /// <summary>Mã lớn, dễ đọc (OTP, mật khẩu tạm)</summary>
    public EmailContent Code(string code, string? caption = null) => Add(new EmailBlock.CodeBox(code, caption));

    /// <summary>Nút hành động chính + link dự phòng cho trình đọc không bấm được nút</summary>
    public EmailContent Button(string label, string url) => Add(new EmailBlock.Action(label, url));

    /// <summary>Bảng nhãn → giá trị (tài khoản, thời gian, thiết bị...)</summary>
    public EmailContent Details(params (string Label, string Value)[] rows) => Add(new EmailBlock.Table(rows));

    public EmailContent Items(IEnumerable<string> items) => Add(new EmailBlock.List(items.ToList()));

    /// <summary>Hộp lưu ý nổi bật</summary>
    public EmailContent Notice(string text, EmailTone tone = EmailTone.Info) => Add(new EmailBlock.Callout(text, tone));

    /// <summary>Dòng chữ nhỏ cuối nội dung (vd "Nếu không phải bạn...")</summary>
    public EmailContent Footnote(string text) => Add(new EmailBlock.Small(text));

    private EmailContent Add(EmailBlock block)
    {
        _blocks.Add(block);
        return this;
    }

    /// <summary>Bản chữ thuần (multipart/alternative) — cũng dùng trong test để đọc mã OTP / link.</summary>
    public string ToPlainText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Heading).AppendLine();
        if (Greeting is not null) sb.AppendLine(Greeting).AppendLine();
        foreach (var block in _blocks)
        {
            switch (block)
            {
                case EmailBlock.Paragraph p: sb.AppendLine(StripBold(p.Text)); break;
                case EmailBlock.CodeBox c: sb.AppendLine($"{(c.Caption is null ? "" : c.Caption + ": ")}{c.Code}"); break;
                case EmailBlock.Action a: sb.AppendLine($"{a.Label}: {a.Url}"); break;
                case EmailBlock.Table t: foreach (var (label, value) in t.Rows) sb.AppendLine($"- {label}: {value}"); break;
                case EmailBlock.List l: foreach (var item in l.Items) sb.AppendLine($"• {StripBold(item)}"); break;
                case EmailBlock.Callout n: sb.AppendLine($"Lưu ý: {StripBold(n.Text)}"); break;
                case EmailBlock.Small s: sb.AppendLine(StripBold(s.Text)); break;
            }
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    public static string StripBold(string text) => BoldRegex().Replace(text, "$1");

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    public static partial Regex BoldRegex();
}

public enum EmailTone
{
    Info,
    Warning,
    Danger,
}

/// <summary>Các khối nội dung email</summary>
public abstract record EmailBlock
{
    public sealed record Paragraph(string Text) : EmailBlock;
    public sealed record CodeBox(string Code, string? Caption) : EmailBlock;
    public sealed record Action(string Label, string Url) : EmailBlock;
    public sealed record Table(IReadOnlyList<(string Label, string Value)> Rows) : EmailBlock;
    public sealed record List(IReadOnlyList<string> Items) : EmailBlock;
    public sealed record Callout(string Text, EmailTone Tone) : EmailBlock;
    public sealed record Small(string Text) : EmailBlock;
}
