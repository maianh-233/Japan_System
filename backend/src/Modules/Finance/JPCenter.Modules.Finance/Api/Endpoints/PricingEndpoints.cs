using JPCenter.Api.Common.Auditing;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Pricing.CheckDiscount;
using JPCenter.Modules.Finance.Application.Pricing.GetBranchPrices;
using JPCenter.Modules.Finance.Application.Pricing.GetDiscounts;
using JPCenter.Modules.Finance.Application.Pricing.GetInstallmentPlans;
using JPCenter.Modules.Finance.Application.Pricing.SaveDiscount;
using JPCenter.Modules.Finance.Application.Pricing.SaveInstallmentPlan;
using JPCenter.Modules.Finance.Application.Pricing.SetBranchPrice;
using JPCenter.Modules.Finance.Application.Pricing.ToggleDiscount;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Finance.Api.Endpoints;

/// <summary>TC-01 → TC-03 — bảng giá chi nhánh, ưu đãi, chính sách đóng đợt (<c>finance.tuition.*</c>)</summary>
public static class PricingEndpoints
{
    public sealed record BranchPriceBody(Guid CourseId, Guid BranchId, decimal? Fee);

    public sealed record DiscountBody(string? Code, string? Name, DiscountKind Kind, decimal Value, List<Guid>? CourseIds, List<Guid>? BranchIds,
        DateOnly From, DateOnly To, int? Limit, string? Note, uint? RowVersion);

    public sealed record ToggleBody(bool Active);

    public sealed record PlanBody(string? Name, List<InstallmentPart>? Parts, decimal MinFee, decimal Surcharge, bool? Active, uint? RowVersion);

    public static void Map(IEndpointRouteBuilder finance)
    {
        const string page = FinanceAccess.Tuition;

        finance.MapGet("/branch-prices", async (ISender s, CancellationToken ct) => (await s.Send(new GetBranchPricesQuery(), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        finance.MapPut("/branch-prices", async (BranchPriceBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SetBranchPriceCommand(b.CourseId, b.BranchId, b.Fee), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Sửa bảng giá", AuditCategory.Finance);

        // ---------- Ưu đãi ----------
        finance.MapGet("/discounts", async (ISender s, CancellationToken ct) => (await s.Send(new GetDiscountsQuery(), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        finance.MapPost("/discounts", async (DiscountBody b, ISender s, CancellationToken ct) =>
                (await s.Send(ToCommand(null, b), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Create)
            .Audited("Tạo ưu đãi", AuditCategory.Finance);
        finance.MapPut("/discounts/{id:guid}", async (Guid id, DiscountBody b, ISender s, CancellationToken ct) =>
                (await s.Send(ToCommand(id, b), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Sửa ưu đãi", AuditCategory.Finance);
        finance.MapPost("/discounts/{id:guid}/toggle", async (Guid id, ToggleBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ToggleDiscountCommand(id, b.Active), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Bật / tắt ưu đãi", AuditCategory.Finance);
        // Kiểm mã khi ghi danh — người ghi danh (Lớp học / Hồ sơ HV / Lead) hoặc kế toán
        finance.MapGet("/discounts/check", async (string? code, Guid? courseId, Guid? branchId, decimal? fee, Guid[]? otherCourseIds, ISender s,
                    CancellationToken ct) =>
                (await s.Send(new CheckDiscountQuery(code, courseId, branchId, fee, otherCourseIds ?? []), ct)).ToHttpResult())
            .RequireAnyPermission((page, PermissionAction.View), ("academic.classes", PermissionAction.Update),
                ("students.profiles", PermissionAction.Update), ("crm.leads", PermissionAction.Update));

        // ---------- Chính sách đóng đợt ----------
        finance.MapGet("/installment-plans", async (ISender s, CancellationToken ct) => (await s.Send(new GetInstallmentPlansQuery(), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);
        finance.MapPost("/installment-plans", async (PlanBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveInstallmentPlanCommand(null, b.Name, b.Parts ?? [], b.MinFee, b.Surcharge, b.Active, null), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Tạo chính sách đóng đợt", AuditCategory.Finance);
        finance.MapPut("/installment-plans/{id:guid}", async (Guid id, PlanBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaveInstallmentPlanCommand(id, b.Name, b.Parts ?? [], b.MinFee, b.Surcharge, b.Active, b.RowVersion), ct))
                .ToHttpResult())
            .RequirePermission(page, PermissionAction.Update)
            .Audited("Sửa chính sách đóng đợt", AuditCategory.Finance);
    }

    private static SaveDiscountCommand ToCommand(Guid? id, DiscountBody b) =>
        new(id, b.Code, b.Name, b.Kind, b.Value, b.CourseIds ?? [], b.BranchIds ?? [], b.From, b.To, b.Limit, b.Note, b.RowVersion);
}
