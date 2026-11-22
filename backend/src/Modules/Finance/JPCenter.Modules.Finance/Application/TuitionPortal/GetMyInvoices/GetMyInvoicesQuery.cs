using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.TuitionPortal.GetMyInvoices;

/// <summary>
/// TC-09 — cổng HV / PH: học phí của chính mình / các con (chủ tài khoản — PQ-11; <paramref name="StudentId"/> lọc 1 con). Không trả người thu,
/// ghi chú nội bộ, Sale (E3); hóa đơn đã hủy không hiện. VietQR = tài khoản chi nhánh + còn nợ + nội dung CK; chi nhánh chưa khai báo → null (E2).
/// </summary>
public sealed record GetMyInvoicesQuery(Guid? StudentId) : IQuery<IReadOnlyList<PortalInvoiceDto>>;

public sealed record PortalInvoiceDto(Guid Id, string Code, Guid StudentId, string StudentName, string? ClassCode, string? ClassName, decimal Amount,
    decimal Discount, decimal Total, decimal Collected, decimal Refunded, decimal Remaining, DateOnly DueDate, DateTimeOffset IssuedAt,
    TuitionStatus Status, IReadOnlyList<PortalPaymentDto> Payments, IReadOnlyList<PortalPaymentDto> Refunds, PortalQrDto? Qr);

public sealed record PortalPaymentDto(decimal Amount, PaymentMethod Method, DateTimeOffset At);

/// <summary>Thông tin tạo mã VietQR (frontend dựng ảnh QR)</summary>
public sealed record PortalQrDto(string BankName, string Account, string Holder, decimal Amount, string Content);

public sealed class GetMyInvoicesHandler(
    IFinanceDbContext db, ICurrentUser user, IGuardianDirectory guardians, IEnrollmentLookup students, IClassLookup classes,
    IBranchDirectory branches, IClock clock) : IQueryHandler<GetMyInvoicesQuery, IReadOnlyList<PortalInvoiceDto>>
{
    public static readonly Error NotYourChild = Error.Forbidden("finance.portal.not_your_student", "Học viên không thuộc tài khoản của bạn");

    public async Task<Result<IReadOnlyList<PortalInvoiceDto>>> Handle(GetMyInvoicesQuery q, CancellationToken ct)
    {
        IReadOnlyList<Guid> mine = user.Access switch
        {
            { Portal: Portal.Student, OwnerId: { } me } => [me],
            { Portal: Portal.Parent, OwnerId: { } lead } => await guardians.ChildrenOfAsync(lead, ct),
            _ => [],
        };
        if (q.StudentId is { } wanted)
        {
            if (!mine.Contains(wanted)) return NotYourChild;
            mine = [wanted];
        }
        if (mine.Count == 0) return Result.Success<IReadOnlyList<PortalInvoiceDto>>([]);

        var list = await db.Invoices.AsNoTracking().Where(i => mine.Contains(i.StudentId) && i.Status != InvoiceStatus.Voided)
            .OrderByDescending(i => i.IssuedAt).ToListAsync(ct);
        var names = await students.NamesAsync(mine.ToList(), ct);
        var cls = await classes.FindManyAsync(list.Select(i => i.ClassId).Distinct().ToList(), ct);
        var bank = new Dictionary<Guid, BranchSummaryLite?>();
        foreach (var b in list.Select(i => i.BranchId.Value).Distinct())
            bank[b] = await branches.FindAsync(b, ct) is { BankAccount: { Length: > 0 } acc } s ? new(s.BankName ?? "", acc, s.BankHolder ?? "") : null;
        var today = clock.Today;

        return list.Select(i =>
        {
            var s = i.Summarize(today);
            cls.TryGetValue(i.ClassId, out var c);
            var account = bank[i.BranchId.Value];
            return new PortalInvoiceDto(i.Id.Value, i.Code, i.StudentId, names.GetValueOrDefault(i.StudentId) ?? "", c?.Code, c?.Name, i.Amount.Amount,
                i.Discount.Amount, s.Total.Amount, s.Collected.Amount, s.Refunded.Amount, s.Remaining.Amount, s.CurrentDueDate ?? i.DueDate, i.IssuedAt,
                s.Status, i.Payments.OrderBy(p => p.PaidAt).Select(p => new PortalPaymentDto(p.Amount.Amount, p.Method, p.PaidAt)).ToList(),
                i.Refunds.OrderBy(r => r.RefundedAt).Select(r => new PortalPaymentDto(r.Amount.Amount, r.Method, r.RefundedAt)).ToList(),
                account is not null && s.Remaining > Money.Zero
                    ? new PortalQrDto(account.BankName, account.Account, account.Holder, s.Remaining.Amount, i.TransferContent)
                    : null);
        }).ToList();
    }

    private sealed record BranchSummaryLite(string BankName, string Account, string Holder);
}
