using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Reconciliation.Common;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Reconciliation.GetMatchSuggestions;

/// <summary>TC-10 bước 3 — gợi ý hóa đơn cho giao dịch chưa khớp (đúng mã +100, trùng tên +40, mã lớp +20, đúng số nợ +30…)</summary>
public sealed record GetMatchSuggestionsQuery(Guid TransactionId) : IQuery<IReadOnlyList<MatchSuggestionDto>>;

public sealed class GetMatchSuggestionsHandler(TransactionFinder finder, BankMatcher matcher)
    : IQueryHandler<GetMatchSuggestionsQuery, IReadOnlyList<MatchSuggestionDto>>
{
    public async Task<Result<IReadOnlyList<MatchSuggestionDto>>> Handle(GetMatchSuggestionsQuery q, CancellationToken ct)
    {
        var found = await finder.FindAsync(q.TransactionId, PermissionAction.View, ct);
        if (found.IsFailure) return found.Error;
        var txn = found.Value;
        var (_, candidates) = await matcher.CandidatesAsync(ct);
        var byId = candidates.ToDictionary(c => c.InvoiceId);
        return MatchScorer.Suggest(txn.Amount, txn.Content, txn.Payer, txn.BranchId, candidates)
            .Select(s =>
            {
                var c = byId[s.InvoiceId];
                return new MatchSuggestionDto(c.InvoiceId.Value, c.Code, c.StudentName, c.ClassCode, c.Remaining.Amount, c.BranchId.Value, s.Score, s.Reasons);
            }).ToList();
    }
}
