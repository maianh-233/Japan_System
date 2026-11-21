using JPCenter.Api.Common.Idempotency;
using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Evaluations.GetEvaluations;
using JPCenter.Modules.Academic.Application.Promotions.AddPromotions;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Academic.Application.Promotions.ConfirmPromotion;
using JPCenter.Modules.Academic.Application.Promotions.GetSalePromotions;
using JPCenter.Modules.Academic.Application.Promotions.SaleConsult;
using JPCenter.Modules.Academic.Application.Promotions.GetPromotions;
using JPCenter.Modules.Academic.Application.Promotions.HandOverPromotions;
using JPCenter.Modules.Academic.Application.Promotions.UpdatePromotion;
using JPCenter.Modules.Academic.Domain.Promotions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>
/// DT-17 lên lớp (tab Lên lớp của chi tiết lớp) + DT-19 xem nhận xét cuối khóa — quyền <c>academic.classes.*</c>.
/// Phía Sale (CRM-19 — ghi chú tư vấn, không liên lạc được, chốt / từ chối) — quyền <c>crm.promotions</c>, dữ liệu vẫn của Academic.
/// </summary>
public static class PromotionEndpoints
{
    public sealed record AddBody(List<Guid> StudentIds, PromotionSource? Source);

    public sealed record UpdateBody(Guid? ToClassId, bool? HoldSeat, DateOnly? HoldUntil);

    public sealed record HandOverBody(List<Guid>? Ids, Guid? SaleId);

    public static void Map(IEndpointRouteBuilder academic)
    {
        const string page = AcademicAccess.Classes;

        academic.MapGet("/classes/{id:guid}/promotions", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetPromotionsQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        academic.MapPost("/classes/{id:guid}/promotions", async (Guid id, AddBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AddPromotionsCommand(id, b.StudentIds ?? [], b.Source ?? PromotionSource.Manual), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        academic.MapPost("/classes/{id:guid}/promotions/hand-over", async (Guid id, HandOverBody? b, ISender s, CancellationToken ct) =>
                (await s.Send(new HandOverPromotionsCommand(id, b?.Ids, b?.SaleId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        academic.MapPut("/promotions/{id:guid}", async (Guid id, UpdateBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdatePromotionCommand(id, b.ToClassId, b.HoldSeat ?? false, b.HoldUntil), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // Nhận xét cuối khóa — chỉ nhân viên (DT-19.E2), GV xem / ghi trên cổng
        academic.MapGet("/classes/{id:guid}/evaluations", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetEvaluationsQuery(id, AsTeacher: false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // ---- CRM-19: phía Sale (trang Tuyển sinh › Tư vấn lên lớp, quyền crm.promotions) ----
        const string sale = SalePromotions.Page;

        academic.MapGet("/promotions/sale", async (ISender s, CancellationToken ct) =>
                (await s.Send(new GetSalePromotionsQuery(), ct)).ToHttpResult())
            .RequirePermission(sale, PermissionAction.View);

        academic.MapPost("/promotions/{id:guid}/notes", async (Guid id, NoteBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaleConsultCommand(id, SaleConsultAction.Note, b.Text, b.Channel), ct)).ToHttpResult())
            .RequirePermission(sale, PermissionAction.Update);

        academic.MapPost("/promotions/{id:guid}/unreachable", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new SaleConsultCommand(id, SaleConsultAction.Unreachable, null, null), ct)).ToHttpResult())
            .RequirePermission(sale, PermissionAction.Update);

        academic.MapPost("/promotions/{id:guid}/decline", async (Guid id, NoteBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SaleConsultCommand(id, SaleConsultAction.Decline, b.Text, null), ct)).ToHttpResult())
            .RequirePermission(sale, PermissionAction.Update);

        academic.MapPost("/promotions/{id:guid}/confirm", async (Guid id, ConfirmBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ConfirmPromotionCommand(id, b.Amount, b.Discount, b.DueDate), ct)).ToHttpResult())
            .RequirePermission(sale, PermissionAction.Update)
            .RequireRateLimiting(RateLimitPolicies.WriteSensitive)
            .Idempotent();
    }

    public sealed record NoteBody(string? Text, string? Channel);

    public sealed record ConfirmBody(decimal? Amount, decimal Discount, DateOnly? DueDate);
}
