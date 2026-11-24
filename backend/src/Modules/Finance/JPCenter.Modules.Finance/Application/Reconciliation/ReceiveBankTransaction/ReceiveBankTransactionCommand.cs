using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Reconciliation.Common;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Reconciliation.ReceiveBankTransaction;

/// <summary>
/// TC-10 bước 1–2 — webhook ngân hàng (chữ ký đã kiểm ở endpoint — E6): tài khoản nhận → chi nhánh (System), lưu giao dịch "chưa khớp" rồi thử tự khớp.
/// Gửi lại cùng <paramref name="Ref"/> → trả giao dịch đã có (E1). Mỗi giao dịch nhận ghi 1 dòng nhật ký tích hợp <c>vietqr</c> (số tiền, khớp / chưa khớp).
/// </summary>
/// <param name="AutoMatch">Việc <c>autoMatch</c> của tích hợp VietQR — tắt thì chỉ lưu "chưa khớp", chờ đối soát tay (TH-07)</param>
public sealed record ReceiveBankTransactionCommand(string? Ref, DateTimeOffset At, decimal Amount, string? Content, string? Payer, string? Account,
    bool AutoMatch = true) : ICommand<BankTxnDto>;

public sealed class ReceiveBankTransactionHandler(IFinanceDbContext db, IBranchDirectory branches, BankMatcher matcher, IIntegrationLog integrations)
    : ICommandHandler<ReceiveBankTransactionCommand, BankTxnDto>
{
    public static readonly Error UnknownAccount = Error.Validation("finance.bank_txn.unknown_account",
        "Tài khoản nhận không thuộc chi nhánh nào");

    public async Task<Result<BankTxnDto>> Handle(ReceiveBankTransactionCommand cmd, CancellationToken ct)
    {
        var reference = cmd.Ref?.Trim();
        if (!string.IsNullOrEmpty(reference) && await db.BankTransactions.FirstOrDefaultAsync(t => t.Ref == reference, ct) is { } existing)
            return await matcher.ToDtoAsync(existing, ct);

        var account = Digits(cmd.Account);
        var branch = (await branches.ListAsync(false, ct)).FirstOrDefault(b => account.Length > 0 && Digits(b.BankAccount) == account);
        if (branch is null)
        {
            await integrations.WriteAsync(BankWebhookGate.IntegrationKey, false, $"Giao dịch {reference}: tài khoản nhận không thuộc chi nhánh nào", ct);
            return UnknownAccount;
        }
        var money = Money.Create(cmd.Amount);
        if (money.IsFailure) return BankTransactionErrors.InvalidAmount;

        var received = BankTransaction.Receive(reference, cmd.At, money.Value, cmd.Content, cmd.Payer, cmd.Account, new BranchId(branch.Id));
        if (received.IsFailure) return received.Error;
        var txn = received.Value;
        db.BankTransactions.Add(txn);
        var matched = cmd.AutoMatch && await matcher.TryAutoMatchAsync(txn, ct);
        await db.SaveChangesAsync(ct);
        var dto = await matcher.ToDtoAsync(txn, ct);
        var note = matched ? $"khớp {string.Join(", ", dto.InvoiceCodes)}" : cmd.AutoMatch ? "chưa khớp" : "chưa khớp (tắt tự khớp)";
        await integrations.WriteAsync(BankWebhookGate.IntegrationKey, true, $"Nhận giao dịch {reference} {MoneyText.Vnd(money.Value.Amount)} — {note}", ct);
        return dto;
    }

    private static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
}
