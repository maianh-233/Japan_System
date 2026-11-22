using JPCenter.Modules.Finance.Api.Endpoints;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Finance.Api;

/// <summary>
/// User story 08 (giai đoạn 19) — <c>/api/v1/finance</c>. Quyền: <c>finance.tuition</c> (giá, ưu đãi, đóng đợt) · <c>finance.invoices</c>
/// (<c>approve</c> = duyệt hoàn / hủy qua hộp duyệt chung) · <c>finance.reconciliation</c> · <c>finance.debts</c>. Cổng HV / PH: theo chủ tài khoản.
/// </summary>
public static class FinanceEndpoints
{
    public static void Map(IEndpointRouteBuilder finance)
    {
        PricingEndpoints.Map(finance);
        InvoiceEndpoints.Map(finance);
        ReconciliationEndpoints.Map(finance);
        DebtEndpoints.Map(finance);
        TuitionPortalEndpoints.Map(finance);
    }
}
