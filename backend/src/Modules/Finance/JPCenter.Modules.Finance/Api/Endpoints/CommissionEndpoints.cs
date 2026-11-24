using JPCenter.Api.Common.Auditing;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Commissions.Common;
using JPCenter.Modules.Finance.Application.Commissions.GetCommissions;
using JPCenter.Modules.Finance.Application.Commissions.ReassignSale;
using JPCenter.Modules.Finance.Application.Commissions.SaveCommissionTiers;
using JPCenter.Modules.Finance.Application.Commissions.SetSaleTarget;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Finance.Api.Endpoints;

/// <summary>TC-12 — hoa hồng & KPI Sale (<c>finance.commissions.*</c>): kết quả tháng, bảng bậc (thay cả bảng), chỉ tiêu, gán lại Sale cho hóa đơn</summary>
public static class CommissionEndpoints
{
    public sealed record TiersBody(List<CommissionTierDto>? Tiers);

    public sealed record TargetBody(Guid EmployeeId, string? Month, decimal Target);

    public sealed record SaleBody(Guid EmployeeId);

    public static void Map(IEndpointRouteBuilder finance)
    {
        const string page = FinanceAccess.Commissions;

        finance.MapGet("/commissions", async (string? month, Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetCommissionsQuery(month, branchId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        finance.MapPut("/commission-tiers", async (TiersBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveCommissionTiersCommand(b.Tiers ?? []), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Sửa bậc hoa hồng", AuditCategory.Finance, AuditSeverity.Warning);
        finance.MapPut("/sale-targets", async (TargetBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SetSaleTargetCommand(b.EmployeeId, b.Month, b.Target), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Đặt chỉ tiêu Sale", AuditCategory.Finance);
        finance.MapPut("/invoices/{id:guid}/sale", async (Guid id, SaleBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ReassignSaleCommand(id, b.EmployeeId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Gán lại Sale cho hóa đơn", AuditCategory.Finance, AuditSeverity.Warning);
    }
}
