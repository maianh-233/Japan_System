using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Reminders;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Tests.Domain;

public sealed class ReconciliationTests
{
    private static readonly BranchId Cg = new(Guid.NewGuid()), Q1 = new(Guid.NewGuid());
    private static readonly DateTimeOffset At = new(2026, 11, 21, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid Accountant = Guid.NewGuid();

    private static Money M(decimal v) => Money.FromTrusted(v);

    private static MatchCandidate Candidate(string code, string name, decimal remaining, BranchId? branch = null, string? cls = null) =>
        new(new InvoiceId(Guid.NewGuid()), code, name, cls, M(remaining), branch ?? Cg);

    [Fact]
    public void TC_10_nhan_giao_dich_khop_bo_khop_bo_qua()
    {
        BankTransaction.Receive(" ", At, M(1), "x", "x", "x", Cg).Error.ShouldBe(BankTransactionErrors.RefRequired);
        BankTransaction.Receive("FT1", At, Money.Zero, "x", "x", "x", Cg).Error.ShouldBe(BankTransactionErrors.InvalidAmount);
        var txn = BankTransaction.Receive(" FT26112100001 ", At, M(1_500_000), "HD-CG-2611-0001 NGUYEN VAN AN", "NGUYEN VAN AN", "0123", Cg).Value;
        (txn.Ref, txn.Status).ShouldBe(("FT26112100001", TransactionStatus.Unmatched));

        txn.Ignore(" ").Error.ShouldBe(BankTransactionErrors.NoteRequired);
        txn.Ignore("Chuyển nội bộ").IsSuccess.ShouldBeTrue();
        txn.Status.ShouldBe(TransactionStatus.Ignored);

        var a = new InvoiceId(Guid.NewGuid());
        var b = new InvoiceId(Guid.NewGuid());
        txn.Match([], Accountant, At).Error.ShouldBe(BankTransactionErrors.InvoicesRequired);
        txn.Match([a, b, a], Accountant, At).IsSuccess.ShouldBeTrue();
        (txn.Status, txn.InvoiceIds.Count, txn.Note).ShouldBe((TransactionStatus.Matched, 2, (string?)null));
        txn.Match([a], null, At).Error.ShouldBe(BankTransactionErrors.AlreadyMatched);
        txn.Ignore("x").Error.ShouldBe(BankTransactionErrors.AlreadyMatched);

        txn.Unmatch().Value.ShouldBe([a, b]);
        (txn.Status, txn.MatchedBy).ShouldBe((TransactionStatus.Unmatched, (Guid?)null));
        txn.Unmatch().Error.ShouldBe(BankTransactionErrors.NotMatched);
        txn.Match([a], null, At).IsSuccess.ShouldBeTrue();
        txn.Status.ShouldBe(TransactionStatus.Auto);
    }

    [Fact]
    public void Bo_qua_nham_thi_dua_ve_chua_khop()
    {
        var txn = BankTransaction.Receive("FT1", At, M(100_000), "ck", "A", "1", Cg).Value;
        txn.Ignore("Chuyển nội bộ").IsSuccess.ShouldBeTrue();
        txn.Unmatch().Value.ShouldBeEmpty();
        (txn.Status, txn.Note).ShouldBe((TransactionStatus.Unmatched, (string?)null));
    }

    [Fact]
    public void TC_10_tu_khop_chi_khi_dung_1_ma_va_khong_vuot_no()
    {
        var an = Candidate("HD-CG-2611-0001", "Nguyễn Văn An", 2_000_000);
        var binh = Candidate("HD-CG-2611-0002", "Trần Bình", 1_000_000);
        var paid = Candidate("HD-CG-2611-0003", "Lê Chi", 0);
        MatchScorer.AutoMatch(M(2_000_000), "hd cg 2611 0001 nguyen van an", [an, binh]).ShouldBe(an);
        MatchScorer.AutoMatch(M(2_000_001), "HD-CG-2611-0001", [an]).ShouldBeNull();
        MatchScorer.AutoMatch(M(500_000), "HD-CG-2611-0001 HD-CG-2611-0002", [an, binh]).ShouldBeNull();
        MatchScorer.AutoMatch(M(500_000), "HD-CG-2611-0003", [paid]).ShouldBeNull();
        MatchScorer.AutoMatch(M(500_000), "hoc phi be An", [an]).ShouldBeNull();
    }

    [Fact]
    public void TC_10_goi_y_cham_diem()
    {
        var an = Candidate("HD-CG-2611-0001", "Nguyễn Văn An", 2_000_000, cls: "N5-K21");
        var anQ1 = Candidate("HD-Q1-2611-0005", "Phạm Thị An", 1_000_000, Q1);
        var other = Candidate("HD-CG-2611-0009", "Lê Văn Hùng", 2_000_000);
        var full = Candidate("HD-CG-2611-0010", "Nguyễn Văn An", 0);

        var s = MatchScorer.Suggest(M(2_000_000), "NOP HOC PHI NGUYEN VAN AN LOP N5-K21", "NGUYEN VAN AN", Cg, [an, anQ1, other, full]);
        // Hóa đơn khác chỉ trùng số còn nợ + cùng chi nhánh (35đ) vẫn được gợi ý, xếp sau; HV Q1 vượt nợ, HV đã đóng đủ bị loại
        s.Select(x => x.InvoiceId).ShouldBe([an.InvoiceId, other.InvoiceId]);
        s[0].Score.ShouldBe(40 + 20 + 30 + 5);
        s[1].Score.ShouldBe(30 + 5);
        s[0].Reasons.ShouldBe(["Trùng tên học viên", "Có mã lớp", "Đúng số còn nợ"]);

        // Chỉ có tên → +10 (+5 ít hơn nợ) chưa đủ 30; vượt nợ −50
        MatchScorer.Suggest(M(500_000), "chuyen tien hoc phi be An", "X", Cg, [an]).ShouldBeEmpty();
        MatchScorer.Suggest(M(3_000_000), "HD-CG-2611-0001", "X", Cg, [an]).Single().Score.ShouldBe(100 - 50 + 5);
    }

    [Fact]
    public void TC_11_moc_tu_nhac_va_tuoi_no()
    {
        var plan = new ReminderPlan();
        var due = new DateOnly(2026, 11, 28);
        plan.Due(due, due.AddDays(-4), []).ShouldBeNull();
        plan.Due(due, due.AddDays(-3), []).ShouldBe(ReminderMilestone.BeforeDue);
        plan.Due(due, due.AddDays(-1), [ReminderMilestone.BeforeDue]).ShouldBeNull();
        plan.Due(due, due, [ReminderMilestone.BeforeDue]).ShouldBe(ReminderMilestone.OnDue);
        // Job lỡ ngày đúng hạn → hôm sau vẫn nhắc bù mốc "đúng hạn", không gửi lại mốc cũ
        plan.Due(due, due.AddDays(2), [ReminderMilestone.BeforeDue]).ShouldBe(ReminderMilestone.OnDue);
        plan.Due(due, due.AddDays(7), [ReminderMilestone.OnDue]).ShouldBe(ReminderMilestone.Overdue);
        plan.Due(due, due.AddDays(20), [ReminderMilestone.Overdue]).ShouldBeNull();

        DebtAgingRule.Of(due, due).ShouldBe(DebtAging.NotDue);
        DebtAgingRule.Of(due, due.AddDays(1)).ShouldBe(DebtAging.Days1To30);
        DebtAgingRule.Of(due, due.AddDays(30)).ShouldBe(DebtAging.Days1To30);
        DebtAgingRule.Of(due, due.AddDays(31)).ShouldBe(DebtAging.Days31To60);
        DebtAgingRule.Of(due, due.AddDays(61)).ShouldBe(DebtAging.Over60);

        var manual = PaymentReminder.Manual(new InvoiceId(Guid.NewGuid()), ReminderChannel.Zalo, Accountant, "  ", At);
        (manual.Note, manual.Milestone, manual.By).ShouldBe(((string?)null, (ReminderMilestone?)null, (Guid?)Accountant));
        PaymentReminder.Auto(new InvoiceId(Guid.NewGuid()), ReminderChannel.Email, ReminderMilestone.OnDue, At).By.ShouldBeNull();
    }
}
