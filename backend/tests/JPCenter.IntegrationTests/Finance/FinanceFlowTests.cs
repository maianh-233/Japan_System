using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Finance.Api.Endpoints;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Finance;

/// <summary>
/// User story 08 (giai đoạn 19 phase 3) — hóa đơn sinh từ ghi danh, thu tiền, hoàn / hủy qua hộp duyệt chung, đối soát VietQR, công nợ & nhắc,
/// bảng giá / ưu đãi / đóng đợt, học phí ở cổng HV.
/// </summary>
[Collection(SeededCollection.Name)]
public sealed class FinanceFlowTests(SeededApiFactory api)
{
    private const string Finance = "/api/v1/finance";
    private const string Approvals = "/api/v1/system/approvals";
    private const string CgAccount = "0011004455667";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _seq;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid ClassN5K21 = SeedData.IdOf("C-N5K21");

    private async Task<HttpClient> SignIn(string email = "superadmin@jpcenter.vn")
    {
        var client = api.CreateClient();
        var r = await client.PostAsJsonAsync("/api/v1/system/auth/login",
            new { identifier = email, password = SeededApiFactory.Password, remember = false }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Json(r)).GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement;

    private static async Task<JsonElement> Ok(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.IsSuccessStatusCode.ShouldBeTrue(body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement;
    }

    private static async Task<string> Code(HttpResponseMessage r) => (await Json(r)).GetProperty("code").GetString()!;

    private static Task<HttpResponseMessage> PostKeyed(HttpClient c, string url, object body) =>
        c.SendAsync(new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body), Headers = { { "Idempotency-Key", Guid.NewGuid().ToString("N") } },
        }, Ct);

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (await condition()) return;
            await Task.Delay(250, Ct);
        }
        (await condition()).ShouldBeTrue("Hết thời gian chờ việc chạy nền");
    }

    /// <summary>HV mới ghi danh lớp N5-K21 (Cầu Giấy) → chờ Finance lập hóa đơn (TC-04)</summary>
    private async Task<(Guid EnrollmentId, JsonElement Invoice)> EnrollWithInvoice(HttpClient admin, decimal discount = 0)
    {
        var name = $"Học viên tài chính {Interlocked.Increment(ref _seq)}{Random.Shared.Next(100, 999)}";
        var created = await Ok(await admin.PostAsJsonAsync("/api/v1/students/import", new { rows = new[] { new { fullName = name, branchId = Cg } } }, Ct));
        var studentId = created.GetProperty("created")[0].GetProperty("id").GetGuid();
        var enrolled = await Ok(await admin.PostAsJsonAsync("/api/v1/students/enrollments",
            new { studentId, classId = ClassN5K21, amount = 6_000_000, discount, dueDate = "2026-12-31" }, Ct));
        var enrollmentId = enrolled.GetProperty("enrollments")[0].GetProperty("id").GetGuid();

        JsonElement invoice = default;
        await Eventually(async () =>
        {
            var list = await Ok(await admin.GetAsync($"{Finance}/invoices?studentId={studentId}", Ct));
            if (list.GetArrayLength() == 0) return false;
            invoice = list[0];
            return true;
        });
        return (enrollmentId, invoice);
    }

    // ---------------- Hóa đơn ----------------

    [Fact(DisplayName = "TC-04: ghi danh → hóa đơn có mã theo mẫu, nội dung CK riêng; giảm trừ; phải thu = 0 thì đã đóng ngay")]
    public async Task Invoice_from_enrollment()
    {
        var admin = await SignIn();
        var (enrollmentId, inv) = await EnrollWithInvoice(admin, discount: 500_000);
        inv.GetProperty("enrollmentId").GetGuid().ShouldBe(enrollmentId);
        var code = inv.GetProperty("code").GetString()!;
        code.ShouldMatch(@"^HD-CG-\d{4}-\d{4}$");
        inv.GetProperty("transferContent").GetString()!.ShouldStartWith(code + " HOC VIEN TAI CHINH");
        (inv.GetProperty("total").GetDecimal(), inv.GetProperty("remaining").GetDecimal()).ShouldBe((5_500_000m, 5_500_000m));
        inv.GetProperty("status").GetString().ShouldBe("unpaid");
        inv.GetProperty("dueDate").GetString().ShouldBe("2026-12-31");

        var (_, free) = await EnrollWithInvoice(admin, discount: 6_000_000);
        free.GetProperty("status").GetString().ShouldBe("paid");
    }

    [Fact(DisplayName = "TC-04.E4: bộ đếm số hóa đơn nguyên tử — lấy số đồng thời không trùng")]
    public async Task Invoice_numbering_is_atomic()
    {
        var branch = new BranchId(Cg);
        var period = $"test-{Guid.NewGuid():N}"[..10];
        var numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var scope = api.Scope();
            return await scope.ServiceProvider.GetRequiredService<IInvoiceNumbering>().NextAsync(branch, period, Ct);
        }));
        numbers.Order().ShouldBe(Enumerable.Range(1, 20));
    }

    [Fact(DisplayName = "TC-06: thu nhiều đợt — vượt nợ / ≤ 0 bị chặn, cần Idempotency-Key, Sale không thu được")]
    public async Task Record_payments()
    {
        var admin = await SignIn();
        var (_, inv) = await EnrollWithInvoice(admin);
        var id = inv.GetProperty("id").GetGuid();
        var url = $"{Finance}/invoices/{id}/payments";

        (await admin.PostAsJsonAsync(url, new { amount = 1_000_000, method = "cash" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Code(await PostKeyed(admin, url, new { amount = 7_000_000, method = "cash" }))).ShouldBe("finance.invoice.overpay");
        (await Code(await PostKeyed(admin, url, new { amount = 0, method = "cash" }))).ShouldBe("finance.invoice.invalid_amount");

        var kt = await SignIn("ketoan@jpcenter.vn");
        var partial = await Ok(await PostKeyed(kt, url, new { amount = 2_000_000, method = "cash", note = "Đợt 1" }));
        (partial.GetProperty("status").GetString(), partial.GetProperty("remaining").GetDecimal()).ShouldBe(("partial", 4_000_000m));
        partial.GetProperty("payments")[0].GetProperty("collector").GetString().ShouldNotBe("—");
        var paid = await Ok(await PostKeyed(kt, url, new { amount = 4_000_000, method = "transfer" }));
        paid.GetProperty("status").GetString().ShouldBe("paid");

        var sale = await SignIn("sale@jpcenter.vn");
        (await PostKeyed(sale, url, new { amount = 1, method = "cash" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TC-07: hoàn tiền từ ngưỡng → phiếu duyệt chung; người tạo không tự duyệt; duyệt → ghi hoàn; rút lại → bỏ khoản chờ")]
    public async Task Refund_with_approval()
    {
        var admin = await SignIn();
        var kt = await SignIn("ketoan@jpcenter.vn");
        var (_, inv) = await EnrollWithInvoice(admin);
        var id = inv.GetProperty("id").GetGuid();
        await Ok(await PostKeyed(kt, $"{Finance}/invoices/{id}/payments", new { amount = 3_000_000, method = "cash" }));

        (await Code(await PostKeyed(kt, $"{Finance}/invoices/{id}/refunds", new { amount = 4_000_000, method = "cash", reason = "Nghỉ" })))
            .ShouldBe("finance.invoice.invalid_refund");
        var first = await Ok(await PostKeyed(kt, $"{Finance}/invoices/{id}/refunds", new { amount = 2_000_000, method = "cash", reason = "Nghỉ học" }));
        var approvalId = first.GetProperty("approval").GetProperty("id").GetGuid();
        first.GetProperty("invoice").GetProperty("refundable").GetDecimal().ShouldBe(1_000_000m);
        first.GetProperty("invoice").GetProperty("requests")[0].GetProperty("status").GetString().ShouldBe("pending");

        // Khoản đang chờ trừ vào hoàn tối đa (E1)
        (await Code(await PostKeyed(kt, $"{Finance}/invoices/{id}/refunds", new { amount = 1_500_000, method = "cash", reason = "Thêm" })))
            .ShouldBe("finance.invoice.invalid_refund");

        // Phiếu hiện ở hộp duyệt chung, nội dung khớp frontend
        var box = (await Ok(await admin.GetAsync($"{Approvals}?status=pending", Ct))).EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == approvalId);
        (box.GetProperty("kind").GetString(), box.GetProperty("payload").GetProperty("method").GetString(),
            box.GetProperty("payload").GetProperty("amount").GetDecimal()).ShouldBe(("refund", "cash", 2_000_000m));

        (await kt.PostAsJsonAsync($"{Approvals}/{approvalId}/approve", new { note = (string?)null }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await Ok(await admin.PostAsJsonAsync($"{Approvals}/{approvalId}/approve", new { note = "OK" }, Ct));
        var after = await Ok(await admin.GetAsync($"{Finance}/invoices/{id}", Ct));
        (after.GetProperty("status").GetString(), after.GetProperty("refunded").GetDecimal(), after.GetProperty("paid").GetDecimal())
            .ShouldBe(("refunded", 2_000_000m, 1_000_000m));
        after.GetProperty("refunds")[0].GetProperty("approvedBy").GetString().ShouldNotBeNull();

        // Rút lại (TC-07.E3)
        var second = await Ok(await PostKeyed(kt, $"{Finance}/invoices/{id}/refunds", new { amount = 500_000, method = "cash", reason = "Thêm" }));
        var secondId = second.GetProperty("approval").GetProperty("id").GetGuid();
        await Ok(await kt.PostAsync($"{Approvals}/{secondId}/withdraw", null, Ct));
        var withdrawn = await Ok(await admin.GetAsync($"{Finance}/invoices/{id}", Ct));
        withdrawn.GetProperty("refundable").GetDecimal().ShouldBe(1_000_000m);
        withdrawn.GetProperty("requests").EnumerateArray().Select(r => r.GetProperty("status").GetString()).ShouldContain("withdrawn");
    }

    [Fact(DisplayName = "TC-08: hủy hóa đơn — còn tiền chưa hoàn bị chặn; duyệt → đã hủy, chỉ hiện khi lọc voided")]
    public async Task Void_invoice()
    {
        var admin = await SignIn();
        var kt = await SignIn("ketoan@jpcenter.vn");
        var (_, inv) = await EnrollWithInvoice(admin);
        var id = inv.GetProperty("id").GetGuid();
        var url = $"{Finance}/invoices/{id}/void-requests";

        (await Code(await kt.PostAsJsonAsync(url, new { reason = "sai" }, Ct))).ShouldBe("finance.invoice.void_reason");
        await Ok(await PostKeyed(kt, $"{Finance}/invoices/{id}/payments", new { amount = 100_000, method = "cash" }));
        (await Code(await kt.PostAsJsonAsync(url, new { reason = "Lập trùng hóa đơn" }, Ct))).ShouldBe("finance.invoice.has_payments");

        var (_, other) = await EnrollWithInvoice(admin);
        var otherId = other.GetProperty("id").GetGuid();
        var requested = await Ok(await kt.PostAsJsonAsync($"{Finance}/invoices/{otherId}/void-requests", new { reason = "Lập trùng hóa đơn" }, Ct));
        (await Code(await kt.PostAsJsonAsync($"{Finance}/invoices/{otherId}/void-requests", new { reason = "Lập trùng lần 2" }, Ct)))
            .ShouldBe("finance.invoice.pending_approval");
        await Ok(await admin.PostAsJsonAsync($"{Approvals}/{requested.GetProperty("approval").GetProperty("id").GetGuid()}/approve", new { }, Ct));

        var voided = await Ok(await admin.GetAsync($"{Finance}/invoices/{otherId}", Ct));
        voided.GetProperty("status").GetString().ShouldBe("voided");
        voided.GetProperty("voided").GetProperty("reason").GetString().ShouldBe("Lập trùng hóa đơn");
        (await Ok(await admin.GetAsync($"{Finance}/invoices", Ct))).EnumerateArray().ShouldNotContain(i => i.GetProperty("id").GetGuid() == otherId);
        (await Ok(await admin.GetAsync($"{Finance}/invoices?status=voided", Ct))).EnumerateArray().ShouldContain(i => i.GetProperty("id").GetGuid() == otherId);
    }

    [Fact(DisplayName = "Cho HV nghỉ → hủy phần học phí còn lại (hết công nợ, tiền đã thu giữ nguyên)")]
    public async Task Drop_cancels_remaining()
    {
        var admin = await SignIn();
        var (enrollmentId, inv) = await EnrollWithInvoice(admin);
        var id = inv.GetProperty("id").GetGuid();
        await Ok(await PostKeyed(admin, $"{Finance}/invoices/{id}/payments", new { amount = 1_000_000, method = "cash" }));
        await Ok(await admin.PostAsJsonAsync($"/api/v1/students/enrollments/{enrollmentId}/drop", new { reason = "Chuyển công tác" }, Ct));

        await Eventually(async () => (await Ok(await admin.GetAsync($"{Finance}/invoices/{id}", Ct))).GetProperty("cancelled").GetBoolean());
        var after = await Ok(await admin.GetAsync($"{Finance}/invoices/{id}", Ct));
        (after.GetProperty("status").GetString(), after.GetProperty("remaining").GetDecimal(), after.GetProperty("paid").GetDecimal())
            .ShouldBe(("cancelled", 0m, 1_000_000m));
        after.GetProperty("cancelReason").GetString()!.ShouldContain("Chuyển công tác");
    }

    // ---------------- Đối soát VietQR ----------------

    private async Task<HttpResponseMessage> Webhook(object body, string? secret = SeededApiFactory.BankWebhookSecret)
    {
        var raw = JsonSerializer.Serialize(body);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{Finance}/integrations/bank/transactions")
        {
            Content = new StringContent(raw, Encoding.UTF8, "application/json"),
        };
        if (secret is not null) req.Headers.Add(ReconciliationEndpoints.SignatureHeader, "sha256=" + ReconciliationEndpoints.Sign(raw, secret));
        return await api.CreateClient().SendAsync(req, Ct);
    }

    [Fact(DisplayName = "TC-10: webhook ký sai 401; đúng mã HĐ → tự khớp; gửi lại cùng ref không ghi 2 lần")]
    public async Task Webhook_auto_match()
    {
        var admin = await SignIn();
        var (_, inv) = await EnrollWithInvoice(admin);
        var id = inv.GetProperty("id").GetGuid();
        var body = new
        {
            @ref = $"FT{Guid.NewGuid():N}"[..16], at = DateTimeOffset.UtcNow, amount = 2_500_000,
            content = inv.GetProperty("transferContent").GetString()!.ToLowerInvariant(), payer = "NGUYEN VAN B", account = CgAccount,
        };

        (await Webhook(body, secret: "sai-khoa")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Webhook(body, secret: null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var txn = await Ok(await Webhook(body));
        (txn.GetProperty("status").GetString(), txn.GetProperty("invoiceId").GetGuid()).ShouldBe(("auto", id));
        (await Ok(await Webhook(body))).GetProperty("id").GetGuid().ShouldBe(txn.GetProperty("id").GetGuid());

        var after = await Ok(await admin.GetAsync($"{Finance}/invoices/{id}", Ct));
        after.GetProperty("collected").GetDecimal().ShouldBe(2_500_000m);
        after.GetProperty("payments")[0].GetProperty("method").GetString().ShouldBe("vietqr");
        after.GetProperty("payments")[0].GetProperty("collector").GetString().ShouldBe("Tự động (VietQR)");
    }

    [Fact(DisplayName = "TC-10: chưa khớp → gợi ý, khớp tay (tách 2 hóa đơn), bỏ khớp, bỏ qua cần ghi chú")]
    public async Task Manual_reconciliation()
    {
        var admin = await SignIn();
        var kt = await SignIn("ketoan@jpcenter.vn");
        var (_, a) = await EnrollWithInvoice(admin);
        var (_, b) = await EnrollWithInvoice(admin);
        var txn = await Ok(await Webhook(new
        {
            @ref = $"FT{Guid.NewGuid():N}"[..16], at = DateTimeOffset.UtcNow, amount = 7_000_000,
            // Đúng mã HĐ nhưng vượt nợ → không tự khớp, vẫn được gợi ý (100 − 50 + 5)
            content = $"dong hoc phi {a.GetProperty("code").GetString()}", payer = "PHU HUYNH", account = CgAccount,
        }));
        txn.GetProperty("status").GetString().ShouldBe("unmatched");
        var txnId = txn.GetProperty("id").GetGuid();

        var suggestions = await Ok(await kt.GetAsync($"{Finance}/bank-transactions/{txnId}/suggestions", Ct));
        suggestions[0].GetProperty("invoiceId").GetGuid().ShouldBe(a.GetProperty("id").GetGuid());

        // 1 hóa đơn: vượt nợ (E3)
        (await Code(await kt.PostAsJsonAsync($"{Finance}/bank-transactions/{txnId}/match", new { invoiceId = a.GetProperty("id").GetGuid() }, Ct)))
            .ShouldBe("finance.invoice.overpay");
        (await Code(await kt.PostAsJsonAsync($"{Finance}/bank-transactions/{txnId}/match", new
        {
            allocations = new[] { new { invoiceId = a.GetProperty("id").GetGuid(), amount = 6_000_000m }, new { invoiceId = b.GetProperty("id").GetGuid(), amount = 500_000m } },
        }, Ct))).ShouldBe("finance.bank_txn.allocation_mismatch");
        var matched = await Ok(await kt.PostAsJsonAsync($"{Finance}/bank-transactions/{txnId}/match", new
        {
            allocations = new[] { new { invoiceId = a.GetProperty("id").GetGuid(), amount = 6_000_000m }, new { invoiceId = b.GetProperty("id").GetGuid(), amount = 1_000_000m } },
        }, Ct));
        (matched.GetProperty("status").GetString(), matched.GetProperty("invoiceIds").GetArrayLength()).ShouldBe(("matched", 2));
        (await Code(await kt.PostAsJsonAsync($"{Finance}/bank-transactions/{txnId}/ignore", new { note = "x" }, Ct))).ShouldBe("finance.bank_txn.already_matched");
        (await Ok(await kt.GetAsync($"{Finance}/invoices/{a.GetProperty("id").GetGuid()}", Ct))).GetProperty("status").GetString().ShouldBe("paid");

        var unmatched = await Ok(await kt.PostAsync($"{Finance}/bank-transactions/{txnId}/unmatch", null, Ct));
        unmatched.GetProperty("status").GetString().ShouldBe("unmatched");
        (await Ok(await kt.GetAsync($"{Finance}/invoices/{b.GetProperty("id").GetGuid()}", Ct))).GetProperty("collected").GetDecimal().ShouldBe(0m);

        (await Code(await kt.PostAsJsonAsync($"{Finance}/bank-transactions/{txnId}/ignore", new { note = " " }, Ct))).ShouldBe("finance.bank_txn.note_required");
        (await Ok(await kt.PostAsJsonAsync($"{Finance}/bank-transactions/{txnId}/ignore", new { note = "Chuyển nội bộ" }, Ct)))
            .GetProperty("status").GetString().ShouldBe("ignored");
        (await Ok(await kt.GetAsync($"{Finance}/bank-transactions?status=ignored", Ct))).EnumerateArray().ShouldContain(t => t.GetProperty("id").GetGuid() == txnId);

        var sale = await SignIn("sale@jpcenter.vn");
        (await sale.GetAsync($"{Finance}/bank-transactions", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------------- Công nợ ----------------

    [Fact(DisplayName = "TC-11: công nợ theo tuổi nợ + người trả; nhắc hàng loạt 1 request, nhắc lại trong ngày có cảnh báo; tự nhắc theo mốc")]
    public async Task Debts_and_reminders()
    {
        var kt = await SignIn("ketoan@jpcenter.vn");
        var debts = await Ok(await kt.GetAsync($"{Finance}/debts", Ct));
        var items = debts.GetProperty("items").EnumerateArray().ToList();
        items.ShouldNotBeEmpty();
        items.ShouldAllBe(d => d.GetProperty("remaining").GetDecimal() > 0);
        var aging = debts.GetProperty("aging");
        (aging.GetProperty("notDue").GetDecimal() + aging.GetProperty("days1To30").GetDecimal() + aging.GetProperty("days31To60").GetDecimal()
            + aging.GetProperty("over60").GetDecimal()).ShouldBe(debts.GetProperty("total").GetDecimal());
        items.ShouldContain(d => d.GetProperty("aging").GetString() != "not_due");

        var ids = items.Take(2).Select(d => d.GetProperty("invoiceId").GetGuid()).ToList();
        var first = await Ok(await kt.PostAsJsonAsync($"{Finance}/reminders", new { invoiceIds = ids, channel = "zalo", note = "Nhắc đóng phí" }, Ct));
        first.GetProperty("sent").GetInt32().ShouldBe(2);
        var again = await Ok(await kt.PostAsJsonAsync($"{Finance}/reminders", new { invoiceIds = ids, channel = "email" }, Ct));
        again.GetProperty("warnings").EnumerateArray().ShouldContain(w => w.GetString()!.Contains("đã được nhắc trong hôm nay"));
        var detail = await Ok(await kt.GetAsync($"{Finance}/invoices/{ids[0]}", Ct));
        detail.GetProperty("reminders").GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);

        // Tự nhắc: chạy 2 lần trong ngày không nhắc trùng mốc
        await Ok(await kt.PostAsync($"{Finance}/reminders/auto", null, Ct));
        (await Ok(await kt.PostAsync($"{Finance}/reminders/auto", null, Ct))).GetInt32().ShouldBe(0);

        var sale = await SignIn("sale@jpcenter.vn");
        (await sale.PostAsJsonAsync($"{Finance}/reminders", new { invoiceIds = ids, channel = "zalo" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------------- Bảng giá / ưu đãi / đóng đợt ----------------

    [Fact(DisplayName = "TC-01 → TC-03: giá chi nhánh, ưu đãi (lỗi %, trùng mã, kiểm mã), chính sách đợt tổng ≠ 100%")]
    public async Task Pricing()
    {
        var kt = await SignIn("ketoan@jpcenter.vn");
        var course = Guid.NewGuid();
        (await Code(await kt.PutAsJsonAsync($"{Finance}/branch-prices", new { courseId = course, branchId = Cg, fee = 0 }, Ct)))
            .ShouldBe("finance.invoice.invalid_amount");
        (await Ok(await kt.PutAsJsonAsync($"{Finance}/branch-prices", new { courseId = course, branchId = Cg, fee = 5_200_000 }, Ct)))
            .GetProperty("fee").GetDecimal().ShouldBe(5_200_000m);
        (await Ok(await kt.GetAsync($"{Finance}/branch-prices", Ct))).EnumerateArray().ShouldContain(p => p.GetProperty("courseId").GetGuid() == course);
        await Ok(await kt.PutAsJsonAsync($"{Finance}/branch-prices", new { courseId = course, branchId = Cg, fee = (decimal?)null }, Ct));
        (await Ok(await kt.GetAsync($"{Finance}/branch-prices", Ct))).EnumerateArray().ShouldNotContain(p => p.GetProperty("courseId").GetGuid() == course);

        var code = $"T{Random.Shared.Next(10000, 99999)}";
        object Discount(decimal value, string kind = "percent") =>
            new { code, name = "Ưu đãi thử", kind, value, courseIds = Array.Empty<Guid>(), branchIds = new[] { Cg }, from = "2026-01-01", to = "2027-12-31", limit = 2 };
        (await Code(await kt.PostAsJsonAsync($"{Finance}/discounts", Discount(60), Ct))).ShouldBe("finance.discount.invalid_percent");
        var d = await Ok(await kt.PostAsJsonAsync($"{Finance}/discounts", Discount(10), Ct));
        (await Code(await kt.PostAsJsonAsync($"{Finance}/discounts", Discount(5), Ct))).ShouldBe("finance.discount.duplicate_code");

        var check = await Ok(await kt.GetAsync($"{Finance}/discounts/check?code={code.ToLowerInvariant()}&courseId={course}&branchId={Cg}&fee=4550000", Ct));
        check.GetProperty("amount").GetDecimal().ShouldBe(455_000m);
        (await Code(await kt.GetAsync($"{Finance}/discounts/check?code={code}&courseId={course}&branchId={SeedData.IdOf("q1")}&fee=1000000", Ct)))
            .ShouldBe("finance.discount.not_available");
        await Ok(await kt.PostAsJsonAsync($"{Finance}/discounts/{d.GetProperty("id").GetGuid()}/toggle", new { active = false }, Ct));
        (await Code(await kt.GetAsync($"{Finance}/discounts/check?code={code}&courseId={course}&branchId={Cg}&fee=1000000", Ct)))
            .ShouldBe("finance.discount.not_available");

        (await Code(await kt.PostAsJsonAsync($"{Finance}/installment-plans",
                new { name = "2 đợt", parts = new[] { new { percent = 50, offsetDays = 0 }, new { percent = 40, offsetDays = 30 } }, minFee = 0, surcharge = 0 }, Ct)))
            .ShouldBe("finance.installment.total");
        var plan = await Ok(await kt.PostAsJsonAsync($"{Finance}/installment-plans",
            new { name = "2 đợt", parts = new[] { new { percent = 50, offsetDays = 30 }, new { percent = 50, offsetDays = -7 } }, minFee = 3_000_000, surcharge = 3 }, Ct));
        plan.GetProperty("parts")[0].GetProperty("offsetDays").GetInt32().ShouldBe(-7);

        var sale = await SignIn("sale@jpcenter.vn");
        (await sale.PostAsJsonAsync($"{Finance}/discounts", Discount(10), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------------- Cổng HV ----------------

    [Fact(DisplayName = "TC-09: cổng HV thấy học phí của mình, không có người thu / ghi chú; VietQR theo chi nhánh; cổng không xem được API nhân viên")]
    public async Task Portal_tuition()
    {
        var student = await SignIn("hocvien@jpcenter.vn");
        var raw = await (await student.GetAsync($"{Finance}/portal/invoices", Ct)).Content.ReadAsStringAsync(Ct);
        var list = JsonDocument.Parse(raw).RootElement;
        list.GetArrayLength().ShouldBeGreaterThan(0);
        list.EnumerateArray().Select(i => i.GetProperty("studentId").GetGuid()).Distinct().ShouldBe([SeedData.IdOf("ST-1")]);
        raw.ShouldNotContain("collector");
        raw.ShouldNotContain("\"note\"");
        list.EnumerateArray().Where(i => i.GetProperty("remaining").GetDecimal() > 0)
            .ShouldAllBe(i => i.GetProperty("qr").GetProperty("account").GetString() != null);

        (await student.GetAsync($"{Finance}/portal/invoices?studentId={SeedData.IdOf("ST-3")}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await student.GetAsync($"{Finance}/invoices", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
