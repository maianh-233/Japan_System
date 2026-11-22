using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JPCenter.Api.Common.Auditing;
using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Reconciliation.GetBankTransactions;
using JPCenter.Modules.Finance.Application.Reconciliation.GetMatchSuggestions;
using JPCenter.Modules.Finance.Application.Reconciliation.IgnoreTransaction;
using JPCenter.Modules.Finance.Application.Reconciliation.MatchTransaction;
using JPCenter.Modules.Finance.Application.Reconciliation.ReceiveBankTransaction;
using JPCenter.Modules.Finance.Application.Reconciliation.UnmatchTransaction;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace JPCenter.Modules.Finance.Api.Endpoints;

/// <summary>
/// TC-10 — đối soát VietQR (<c>finance.reconciliation.*</c>) + webhook ngân hàng <c>POST integrations/bank/transactions</c>
/// (không đăng nhập; header <c>X-Signature: sha256=&lt;hex HMAC-SHA256(body, Finance:BankWebhookSecret)&gt;</c> — sai → 401, E6).
/// </summary>
public static class ReconciliationEndpoints
{
    public const string SignatureHeader = "X-Signature";
    public const string SecretKey = "Finance:BankWebhookSecret";

    public sealed record BankWebhookBody(string? Ref, DateTimeOffset At, decimal Amount, string? Content, string? Payer, string? Account);

    public sealed record MatchBody(List<MatchAllocation>? Allocations, Guid? InvoiceId);

    public sealed record NoteBody(string? Note);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Error BadSignature = Error.Unauthorized("finance.bank_webhook.signature", "Chữ ký webhook không hợp lệ");

    public static void Map(IEndpointRouteBuilder finance)
    {
        const string page = FinanceAccess.Reconciliation;

        finance.MapPost("/integrations/bank/transactions", async (HttpRequest request, IConfiguration config, ISender s, CancellationToken ct) =>
            {
                using var reader = new StreamReader(request.Body, Encoding.UTF8);
                var raw = await reader.ReadToEndAsync(ct);
                if (!Verify(raw, request.Headers[SignatureHeader].ToString(), config[SecretKey])) return BadSignature.ToProblem();
                BankWebhookBody? b;
                try
                {
                    b = JsonSerializer.Deserialize<BankWebhookBody>(raw, Json);
                }
                catch (JsonException)
                {
                    b = null;
                }
                if (b is null) return Error.Validation("finance.bank_webhook.invalid_body", "Nội dung webhook không hợp lệ").ToProblem();
                return (await s.Send(new ReceiveBankTransactionCommand(b.Ref, b.At, b.Amount, b.Content, b.Payer, b.Account), ct)).ToHttpResult();
            })
            .AllowAnonymous();

        var g = finance.MapGroup("/bank-transactions");
        g.MapGet("/", async (QueryEnum<TransactionStatus>? status, Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetBankTransactionsQuery(status?.Value, branchId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        g.MapGet("/{id:guid}/suggestions", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetMatchSuggestionsQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        // 1 hóa đơn: { invoiceId } — tách nhiều hóa đơn: { allocations: [{ invoiceId, amount }] }
        g.MapPost("/{id:guid}/match", async (Guid id, MatchBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new MatchTransactionCommand(id, b.Allocations ?? (b.InvoiceId is { } one ? [new MatchAllocation(one, null)] : [])), ct))
                .ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Khớp giao dịch", AuditCategory.Finance);
        g.MapPost("/{id:guid}/ignore", async (Guid id, NoteBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new IgnoreTransactionCommand(id, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Bỏ qua giao dịch", AuditCategory.Finance);
        g.MapPost("/{id:guid}/unmatch", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new UnmatchTransactionCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Bỏ khớp giao dịch", AuditCategory.Finance, AuditSeverity.Warning);
    }

    /// <summary>So chữ ký HMAC-SHA256 (thời gian cố định). Chưa cấu hình khóa → từ chối mọi webhook.</summary>
    public static bool Verify(string body, string header, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || !header.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;
        var expected = Sign(body, secret);
        var given = Encoding.ASCII.GetBytes(header["sha256=".Length..].Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), given);
    }

    /// <summary>Hex chữ thường của HMAC-SHA256(body) — ngân hàng / test ký theo cách này</summary>
    public static string Sign(string body, string secret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
}
