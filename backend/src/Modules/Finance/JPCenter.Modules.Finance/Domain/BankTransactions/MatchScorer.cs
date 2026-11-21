using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Finance.Domain.Common;
using JPCenter.Modules.Finance.Domain.Invoices;

namespace JPCenter.Modules.Finance.Domain.BankTransactions;

/// <summary>
/// Đối soát VietQR (TC-10 bước 2–3) — thuần, không truy cập DB.
/// Tự khớp: nội dung nhắc ĐÚNG 1 mã hóa đơn còn nợ và số tiền ≤ còn nợ. Gợi ý: chấm điểm (mã +100, trùng tên HV +40 / có tên +10,
/// mã lớp +20, đúng số còn nợ +30 / ít hơn +5 / vượt −50, cùng chi nhánh +5), lấy ≥ 30, tối đa 5 (khớp với <c>suggestMatches</c> frontend).
/// </summary>
public static class MatchScorer
{
    public const int MinScore = 30;
    public const int MaxSuggestions = 5;

    /// <summary>Hóa đơn tự khớp được; null = để kế toán xử lý</summary>
    public static MatchCandidate? AutoMatch(Money amount, string content, IEnumerable<MatchCandidate> candidates)
    {
        var mentioned = candidates.Where(c => c.Remaining > Money.Zero && InvoiceCode.Mentions(content, c.Code)).ToList();
        return mentioned.Count == 1 && amount <= mentioned[0].Remaining ? mentioned[0] : null;
    }

    public static IReadOnlyList<MatchSuggestion> Suggest(Money amount, string content, string payer, BranchId branchId,
        IEnumerable<MatchCandidate> candidates)
    {
        var text = VietnameseText.Plain(content);
        var payerText = VietnameseText.Plain(payer);
        return candidates
            .Where(c => c.Remaining > Money.Zero) // E4 — không gợi ý hóa đơn đã đủ
            .Select(c => Score(c, amount, content, text, payerText, branchId))
            .Where(s => s.Score >= MinScore)
            .OrderByDescending(s => s.Score)
            .Take(MaxSuggestions)
            .ToList();
    }

    private static MatchSuggestion Score(MatchCandidate c, Money amount, string content, string text, string payerText, BranchId branchId)
    {
        var score = 0;
        var reasons = new List<string>();
        void Hit(int points, string reason)
        {
            score += points;
            reasons.Add(reason);
        }

        if (InvoiceCode.Mentions(content, c.Code)) Hit(100, "Đúng mã hóa đơn");
        var name = VietnameseText.Plain(c.StudentName);
        var last = name.Split(' ').LastOrDefault() ?? "";
        if (name.Length > 0 && (text.Contains(name, StringComparison.Ordinal) || payerText == name)) Hit(40, "Trùng tên học viên");
        else if (last.Length > 2 && text.Contains(last, StringComparison.Ordinal)) Hit(10, $"Có tên \"{last}\"");
        if (c.ClassCode is { Length: > 0 } cls && text.Contains(VietnameseText.Plain(cls), StringComparison.Ordinal)) Hit(20, "Có mã lớp");
        if (amount == c.Remaining) Hit(30, "Đúng số còn nợ");
        else score += amount < c.Remaining ? 5 : -50;
        if (c.BranchId == branchId) score += 5;
        return new MatchSuggestion(c.InvoiceId, score, reasons);
    }
}
