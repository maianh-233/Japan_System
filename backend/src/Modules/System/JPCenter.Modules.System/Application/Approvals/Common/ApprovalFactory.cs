using System.Text.Json;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Approvals;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Approvals.Common;

/// <summary>Tạo yêu cầu duyệt 2 người (PQ-09 bước 1). Mã hiển thị: DY-yyMM-0001 (đếm trong tháng). Người gọi tự SaveChanges.</summary>
public sealed class ApprovalFactory(ISystemDbContext db, IClock clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ApprovalRequest> FinancePermissionAsync(UserAccess requester, string title, string? detail,
        FinancePermissionPayload payload, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var seq = await db.ApprovalRequests.CountAsync(x => x.RequestedAt >= monthStart, ct) + 1;
        var code = $"DY-{now:yyMM}-{seq:0000}";

        var request = ApprovalRequest.Create(code, ApprovalKind.FinancePermission, title, detail, null,
            JsonSerializer.Serialize(payload, Json), requester.AccountId, requester.DisplayName, now);
        db.ApprovalRequests.Add(request);
        return request;
    }

    public static FinancePermissionPayload Read(ApprovalRequest request) =>
        JsonSerializer.Deserialize<FinancePermissionPayload>(request.Payload, Json)!;
}
