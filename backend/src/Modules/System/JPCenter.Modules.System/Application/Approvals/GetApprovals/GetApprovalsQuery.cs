using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Approvals.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Approvals.GetApprovals;

/// <summary>PQ-09 bước 2 — yêu cầu duyệt: của mình + loại mình duyệt được.</summary>
/// <param name="Status">"pending" · "approved" · "rejected" · "withdrawn" — trống = tất cả</param>
public sealed record GetApprovalsQuery(string? Status) : IQuery<IReadOnlyList<ApprovalDto>>;

public sealed class GetApprovalsHandler(ApprovalReader reader, ICurrentUser user) : IQueryHandler<GetApprovalsQuery, IReadOnlyList<ApprovalDto>>
{
    public async Task<Result<IReadOnlyList<ApprovalDto>>> Handle(GetApprovalsQuery query, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(user, query.Status, ct));
}
