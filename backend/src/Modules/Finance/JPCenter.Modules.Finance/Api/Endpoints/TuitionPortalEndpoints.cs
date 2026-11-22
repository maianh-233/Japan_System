using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.TuitionPortal.GetMyInvoices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Finance.Api.Endpoints;

/// <summary>TC-09 — học phí ở cổng HV / PH <c>/portal/invoices</c> (chủ tài khoản; không có ở cổng GV — quy tắc vàng 5)</summary>
public static class TuitionPortalEndpoints
{
    public static void Map(IEndpointRouteBuilder finance)
    {
        finance.MapGet("/portal/invoices", async (Guid? studentId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetMyInvoicesQuery(studentId), ct)).ToHttpResult())
            .RequirePortal(Portal.Student, Portal.Parent);
    }
}
