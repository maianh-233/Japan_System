using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Reconciliation.Common;

/// <summary>
/// Đối soát (TC-10): ứng viên = hóa đơn đang mở còn nợ (mọi chi nhánh — mã hóa đơn đã có tiền tố chi nhánh), tự khớp / gợi ý bằng <see cref="MatchScorer"/>,
/// ghi khoản thu VietQR gắn giao dịch. Dựng <see cref="BankTxnDto"/> (mã hóa đơn, tên người khớp).
/// </summary>
public sealed class BankMatcher(IFinanceDbContext db, IEnrollmentLookup students, IClassLookup classes, PeopleNames people, IClock clock)
{
    /// <summary>Hóa đơn đang mở còn nợ (theo dõi thay đổi để ghi thu) + thông tin chấm điểm</summary>
    public async Task<(IReadOnlyDictionary<InvoiceId, Invoice> Invoices, IReadOnlyList<MatchCandidate> Candidates)> CandidatesAsync(CancellationToken ct)
    {
        var open = (await db.Invoices.Where(i => i.Status == InvoiceStatus.Open).ToListAsync(ct)).Where(i => i.Remaining > Money.Zero).ToList();
        var names = await students.NamesAsync(open.Select(i => i.StudentId).Distinct().ToList(), ct);
        var cls = await classes.FindManyAsync(open.Select(i => i.ClassId).Distinct().ToList(), ct);
        var candidates = open.Select(i => new MatchCandidate(i.Id, i.Code, names.GetValueOrDefault(i.StudentId) ?? "",
            cls.TryGetValue(i.ClassId, out var c) ? c.Code : null, i.Remaining, i.BranchId)).ToList();
        return (open.ToDictionary(i => i.Id), candidates);
    }

    /// <summary>TC-10 bước 2 — tự khớp khi nội dung có đúng 1 mã hóa đơn hợp lệ và số tiền ≤ còn nợ. Người gọi tự lưu.</summary>
    public async Task<bool> TryAutoMatchAsync(BankTransaction txn, CancellationToken ct)
    {
        var (invoices, candidates) = await CandidatesAsync(ct);
        if (MatchScorer.AutoMatch(txn.Amount, txn.Content, candidates) is not { } hit) return false;
        var invoice = invoices[hit.InvoiceId];
        if (invoice.Pay(txn.Amount, PaymentMethod.Vietqr, txn.At, null, txn.Id.Value, $"Tự khớp giao dịch {txn.Ref}").IsFailure) return false;
        invoice.ClearDomainEvents();
        return txn.Match([invoice.Id], null, clock.UtcNow).IsSuccess;
    }

    public async Task<IReadOnlyList<BankTxnDto>> ToDtosAsync(IReadOnlyList<BankTransaction> txns, CancellationToken ct)
    {
        var invoiceIds = txns.SelectMany(t => t.InvoiceIds).Distinct().ToList();
        var codes = invoiceIds.Count == 0
            ? new Dictionary<InvoiceId, string>()
            : await db.Invoices.IgnoreQueryFilters().AsNoTracking().Where(i => invoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Code, ct);
        var who = await people.OfAsync(txns.Select(t => t.MatchedBy), ct);
        return txns.Select(t => new BankTxnDto(t.Id.Value, t.Ref, t.At, t.Amount.Amount, t.Content, t.Payer, t.Account, t.BranchId.Value, t.Status,
            t.InvoiceIds.FirstOrDefault() is { Value: var first } && first != Guid.Empty ? first : null,
            t.InvoiceIds.Select(i => i.Value).ToList(), t.InvoiceIds.Select(i => codes.GetValueOrDefault(i) ?? "—").ToList(),
            t.MatchedBy is { } by ? who.GetValueOrDefault(by) ?? "—" : null, t.MatchedAt, t.Note)).ToList();
    }

    public async Task<BankTxnDto> ToDtoAsync(BankTransaction txn, CancellationToken ct) => (await ToDtosAsync([txn], ct))[0];
}
