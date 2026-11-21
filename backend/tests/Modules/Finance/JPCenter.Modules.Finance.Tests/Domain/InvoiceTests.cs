using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Invoices.Events;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Tests.Domain;

public sealed class InvoiceTests
{
    private static readonly BranchId Branch = new(Guid.NewGuid());
    private static readonly Guid Staff = Guid.NewGuid(), Checker = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 11, 21, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Due = new(2026, 11, 28);

    private static Money M(decimal v) => Money.FromTrusted(v);

    private static Invoice New(decimal amount = 3_000_000, decimal discount = 0, IReadOnlyList<InstallmentDue>? schedule = null) =>
        Invoice.Issue("HD-CG-2611-0001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Branch, M(amount), M(discount), null, Due,
            "HD-CG-2611-0001 NGUYEN VAN AN", At, schedule: schedule).Value;

    [Fact]
    public void TC_04_lap_hoa_don_phai_thu_va_su_kien()
    {
        var inv = New(3_000_000, 300_000);
        inv.Total.ShouldBe(M(2_700_000));
        inv.Status.ShouldBe(InvoiceStatus.Open);
        inv.Summarize(new DateOnly(2026, 11, 21)).Status.ShouldBe(TuitionStatus.Unpaid);
        inv.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<InvoiceIssued>().Total.ShouldBe(M(2_700_000));
    }

    [Fact]
    public void TC_03_E6_giam_lon_hon_hoc_phi_va_TC_04_E3_phai_thu_0_la_da_dong()
    {
        Invoice.Issue("x", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Branch, M(100), M(200), null, Due, "x", At)
            .Error.ShouldBe(InvoiceErrors.DiscountTooLarge);
        var free = New(2_000_000, 2_000_000);
        free.Summarize(Due).Status.ShouldBe(TuitionStatus.Paid);
        free.DomainEvents.OfType<InvoicePaid>().ShouldHaveSingleItem();
    }

    [Fact]
    public void TC_06_thu_nhieu_dot_het_no_va_cac_loi()
    {
        var inv = New();
        inv.Pay(M(0), PaymentMethod.Cash, At, Staff).Error.ShouldBe(InvoiceErrors.InvalidAmount);
        inv.Pay(M(3_000_001), PaymentMethod.Cash, At, Staff).Error.ShouldBe(InvoiceErrors.Overpay);
        inv.Pay(M(1_000_000), PaymentMethod.Cash, At, Staff, note: " đợt 1 ").Value.Note.ShouldBe("đợt 1");
        inv.Summarize(Due).Status.ShouldBe(TuitionStatus.Partial);
        inv.Pay(M(2_000_000), PaymentMethod.Vietqr, At, null, Guid.NewGuid());
        var sum = inv.Summarize(Due);
        (sum.Status, sum.Remaining, sum.Collected).ShouldBe((TuitionStatus.Paid, Money.Zero, M(3_000_000)));
        inv.DomainEvents.OfType<PaymentRecorded>().Count().ShouldBe(2);
        inv.DomainEvents.OfType<InvoicePaid>().ShouldHaveSingleItem();
        inv.Pay(M(1), PaymentMethod.Cash, At, Staff).Error.ShouldBe(InvoiceErrors.Overpay);
    }

    [Fact]
    public void TC_11_qua_han_tinh_khi_doc()
    {
        var inv = New();
        inv.Summarize(Due).Status.ShouldBe(TuitionStatus.Unpaid);
        inv.Summarize(Due.AddDays(1)).Status.ShouldBe(TuitionStatus.Overdue);
        inv.Pay(M(500_000), PaymentMethod.Cash, At, Staff);
        inv.Summarize(Due.AddDays(1)).Status.ShouldBe(TuitionStatus.Overdue);
    }

    [Fact]
    public void TC_02_nhieu_dot_han_theo_dot_chua_dong_du()
    {
        var d1 = new DateOnly(2026, 11, 25);
        var d2 = new DateOnly(2026, 12, 25);
        var inv = New(3_000_000, 0, [new(M(1_500_000), d2), new(M(1_500_000), d1)]);
        inv.DueDate.ShouldBe(d2);
        inv.CurrentDueDate().ShouldBe(d1);
        inv.Summarize(d1.AddDays(1)).Status.ShouldBe(TuitionStatus.Overdue);
        inv.Pay(M(1_500_000), PaymentMethod.Transfer, At, Staff);
        inv.CurrentDueDate().ShouldBe(d2);
        inv.Summarize(d1.AddDays(1)).Status.ShouldBe(TuitionStatus.Partial);
        Invoice.Issue("x", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Branch, M(3_000_000), M(0), null, Due, "x", At,
            schedule: [new(M(1_000_000), d1), new(M(1_000_000), d2)]).Error.ShouldBe(InvoiceErrors.InvalidSchedule);
    }

    [Fact]
    public void TC_07_hoan_duoi_nguong_ghi_ngay_hoan_khong_lam_tang_no()
    {
        var inv = New();
        inv.Pay(M(3_000_000), PaymentMethod.Cash, At, Staff);
        inv.RequestRefund(M(500_000), PaymentMethod.Cash, " ", Staff, false, At).Error.ShouldBe(InvoiceErrors.ReasonRequired);
        inv.RequestRefund(M(3_000_001), PaymentMethod.Cash, "Nghỉ", Staff, false, At).Error.ShouldBe(InvoiceErrors.InvalidRefund);
        var done = inv.RequestRefund(M(500_000), PaymentMethod.Cash, "HV nghỉ 2 buổi", Staff, false, At).Value;
        done.Refund.ShouldNotBeNull().ApprovedBy.ShouldBeNull();
        var sum = inv.Summarize(Due);
        (sum.Status, sum.Paid, sum.Remaining, sum.Refundable).ShouldBe((TuitionStatus.Refunded, M(2_500_000), Money.Zero, M(2_500_000)));
        inv.DomainEvents.OfType<RefundIssued>().ShouldHaveSingleItem();
    }

    [Fact]
    public void TC_07_tu_nguong_cho_duyet_tru_hoan_toi_da_nguoi_tao_khong_tu_duyet()
    {
        var inv = New();
        inv.Pay(M(2_000_000), PaymentMethod.Cash, At, Staff);
        var req = inv.RequestRefund(M(1_500_000), PaymentMethod.Transfer, "Lớp hủy", Staff, true, At).Value.Request.ShouldNotBeNull();
        inv.Refundable.ShouldBe(M(500_000));
        inv.Refunds.ShouldBeEmpty();
        inv.RequestRefund(M(600_000), PaymentMethod.Cash, "Thêm", Staff, true, At).Error.ShouldBe(InvoiceErrors.InvalidRefund);

        inv.ApproveRequest(req.Id, Staff, At).Error.ShouldBe(InvoiceErrors.SelfApproval);
        inv.ApproveRequest(req.Id, Checker, At).IsSuccess.ShouldBeTrue();
        var refund = inv.Refunds.ShouldHaveSingleItem();
        (refund.By, refund.ApprovedBy, refund.Amount).ShouldBe((Staff, Checker, M(1_500_000)));
        inv.ApproveRequest(req.Id, Checker, At).Error.ShouldBe(InvoiceErrors.RequestNotPending);
        inv.Refundable.ShouldBe(M(500_000));
    }

    [Fact]
    public void TC_07_E3_rut_lai_va_tu_choi()
    {
        var inv = New();
        inv.Pay(M(1_000_000), PaymentMethod.Cash, At, Staff);
        var a = inv.RequestRefund(M(400_000), PaymentMethod.Cash, "A", Staff, true, At).Value.Request!;
        var b = inv.RequestRefund(M(400_000), PaymentMethod.Cash, "B", Staff, true, At).Value.Request!;
        inv.WithdrawRequest(a.Id, Checker, At).Error.ShouldBe(InvoiceErrors.OnlyRequester);
        inv.WithdrawRequest(a.Id, Staff, At).IsSuccess.ShouldBeTrue();
        inv.RejectRequest(b.Id, Staff, "x", At).Error.ShouldBe(InvoiceErrors.SelfApproval);
        inv.RejectRequest(b.Id, Checker, "Không đúng chính sách", At).IsSuccess.ShouldBeTrue();
        (a.Status, b.Status, b.DecisionNote).ShouldBe((InvoiceRequestStatus.Withdrawn, InvoiceRequestStatus.Rejected, "Không đúng chính sách"));
        inv.Refundable.ShouldBe(M(1_000_000));
    }

    [Fact]
    public void TC_08_huy_hoa_don_qua_duyet_va_khoi_phuc()
    {
        var inv = New();
        inv.RequestVoid("sai", Staff, At).Error.ShouldBe(InvoiceErrors.VoidReasonTooShort);
        var req = inv.RequestVoid("Lập trùng hóa đơn", Staff, At).Value;
        inv.RequestVoid("Lập trùng lần 2", Staff, At).Error.ShouldBe(InvoiceErrors.PendingApproval);
        inv.LinkApproval(req.Id, Guid.NewGuid()).IsSuccess.ShouldBeTrue();
        req.ApprovalId.ShouldNotBeNull();

        inv.ApproveRequest(req.Id, Staff, At).Error.ShouldBe(InvoiceErrors.SelfApproval);
        inv.ApproveRequest(req.Id, Checker, At).IsSuccess.ShouldBeTrue();
        (inv.Status, inv.VoidRequestedBy, inv.VoidApprovedBy).ShouldBe((InvoiceStatus.Voided, Staff, Checker));
        inv.Summarize(Due).Status.ShouldBe(TuitionStatus.Voided);
        inv.Remaining.ShouldBe(Money.Zero);
        inv.DomainEvents.OfType<InvoiceVoided>().ShouldHaveSingleItem();
        inv.Pay(M(1), PaymentMethod.Cash, At, Staff).Error.ShouldBe(InvoiceErrors.Closed);
        inv.RequestVoid("Lập trùng hóa đơn", Staff, At).Error.ShouldBe(InvoiceErrors.Closed);

        inv.RestoreVoided().IsSuccess.ShouldBeTrue();
        (inv.Status, inv.Code).ShouldBe((InvoiceStatus.Open, "HD-CG-2611-0001"));
        inv.Remaining.ShouldBe(M(3_000_000));
        inv.RestoreVoided().Error.ShouldBe(InvoiceErrors.NotVoided);
    }

    [Fact]
    public void TC_08_E1_con_tien_da_thu_chua_hoan_thi_khong_huy()
    {
        var inv = New();
        inv.Pay(M(1_000_000), PaymentMethod.Cash, At, Staff);
        inv.RequestVoid("Lập trùng hóa đơn", Staff, At).Error.ShouldBe(InvoiceErrors.HasPayments);
        inv.RequestRefund(M(1_000_000), PaymentMethod.Cash, "Hoàn hết để hủy", Staff, false, At);
        inv.RequestVoid("Lập trùng hóa đơn", Staff, At).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Huy_phan_con_lai_het_cong_no_tien_da_thu_giu_nguyen()
    {
        var inv = New();
        inv.Pay(M(1_000_000), PaymentMethod.Cash, At, Staff);
        inv.CancelRemaining(null, At).Error.ShouldBe(InvoiceErrors.ReasonRequired);
        inv.CancelRemaining("HV nghỉ học", At).IsSuccess.ShouldBeTrue();
        var sum = inv.Summarize(Due.AddDays(10));
        (sum.Status, sum.Remaining, sum.Paid, sum.CurrentDueDate).ShouldBe((TuitionStatus.Cancelled, Money.Zero, M(1_000_000), (DateOnly?)null));
        inv.CancelRemaining("lần 2", At).Error.ShouldBe(InvoiceErrors.Closed);
        // Hủy phần còn lại vẫn hoàn được tiền đã thu
        inv.RequestRefund(M(1_000_000), PaymentMethod.Cash, "Hoàn", Staff, false, At).IsSuccess.ShouldBeTrue();
        inv.DomainEvents.OfType<InvoiceCancelled>().ShouldHaveSingleItem();
    }

    [Fact]
    public void TC_10_bo_khop_xoa_khoan_thu_cua_giao_dich()
    {
        var inv = New();
        var txn = Guid.NewGuid();
        inv.Pay(M(1_000_000), PaymentMethod.Vietqr, At, null, txn);
        inv.Pay(M(500_000), PaymentMethod.Cash, At, Staff);
        inv.RemoveBankPayments(txn).Value.ShouldBe(M(1_000_000));
        inv.Payments.ShouldHaveSingleItem().Method.ShouldBe(PaymentMethod.Cash);
        inv.RemoveBankPayments(txn).Error.ShouldBe(InvoiceErrors.PaymentNotFound);
    }

    [Fact]
    public void Ma_hoa_don_va_noi_dung_chuyen_khoan()
    {
        var date = new DateOnly(2026, 11, 3);
        InvoiceCode.Format("HD-{CN}-{YY}{MM}-{SO:4}", "cg", date, 7).ShouldBe("HD-CG-2611-0007");
        InvoiceCode.Format("{YYYY}/{SO}", "Q1", date, 12).ShouldBe("2026/12");
        InvoiceCode.PeriodKey("month", date).ShouldBe("2026-11");
        InvoiceCode.PeriodKey("year", date).ShouldBe("2026");
        InvoiceCode.PeriodKey("never", date).ShouldBe("all");

        InvoiceCode.TransferContent("HD-CG-2611-0007", "  Nguyễn  Văn Đức ").ShouldBe("HD-CG-2611-0007 NGUYEN VAN DUC");
        InvoiceCode.TransferContent("HD-CG-2611-0007", new string('a', 80)).Length.ShouldBe(InvoiceCode.MaxTransferContent);
        InvoiceCode.Mentions("chuyen tien hd cg 2611 0007 nguyen van duc", "HD-CG-2611-0007").ShouldBeTrue();
        InvoiceCode.Mentions("HD-CG-2611-0008", "HD-CG-2611-0007").ShouldBeFalse();
    }
}
