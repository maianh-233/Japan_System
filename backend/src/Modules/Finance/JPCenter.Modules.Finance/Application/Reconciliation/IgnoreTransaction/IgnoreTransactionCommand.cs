using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Reconciliation.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Reconciliation.IgnoreTransaction;

/// <summary>TC-10 bước 4 — bỏ qua giao dịch không phải học phí (hoàn ứng, chuyển nội bộ…), ghi chú bắt buộc</summary>
public sealed record IgnoreTransactionCommand(Guid TransactionId, string? Note) : ICommand<BankTxnDto>;

public sealed class IgnoreTransactionHandler(IFinanceDbContext db, TransactionFinder finder, BankMatcher matcher)
    : ICommandHandler<IgnoreTransactionCommand, BankTxnDto>
{
    public async Task<Result<BankTxnDto>> Handle(IgnoreTransactionCommand cmd, CancellationToken ct)
    {
        var found = await finder.FindAsync(cmd.TransactionId, PermissionAction.Update, ct);
        if (found.IsFailure) return found.Error;
        if (found.Value.Ignore(cmd.Note) is { IsFailure: true } f) return f.Error;
        await db.SaveChangesAsync(ct);
        return await matcher.ToDtoAsync(found.Value, ct);
    }
}
