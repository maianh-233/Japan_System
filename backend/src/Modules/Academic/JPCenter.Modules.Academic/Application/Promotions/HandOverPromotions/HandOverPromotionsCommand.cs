using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Promotions.HandOverPromotions;

/// <summary>
/// DT-17 bước 4 / E4 — "Giao Sale ngay" trước khi lớp kết thúc (lớp kết thúc thì tự giao). <paramref name="Ids"/> rỗng = mọi đề xuất chưa chốt của lớp;
/// <paramref name="SaleId"/> null = giữ Sale cũ. ⏳ Tự chọn Sale phụ trách lead liên kết cần Crm (giai đoạn 16).
/// </summary>
public sealed record HandOverPromotionsCommand(Guid ClassId, IReadOnlyList<Guid>? Ids, Guid? SaleId) : ICommand<IReadOnlyList<PromotionDto>>;

public sealed class HandOverPromotionsHandler(IAcademicDbContext db, ClassReader classes, PromotionReader reader, IClock clock, IAuditTrail audit)
    : ICommandHandler<HandOverPromotionsCommand, IReadOnlyList<PromotionDto>>
{
    public async Task<Result<IReadOnlyList<PromotionDto>>> Handle(HandOverPromotionsCommand cmd, CancellationToken ct)
    {
        var cls = await classes.ForUpdateAsync(cmd.ClassId, AcademicAccess.Classes, PermissionAction.Update, ct);
        if (cls is null) return ClassErrors.NotFound;
        var list = await db.PromotionCandidates.Where(p => p.FromClassId == cls.Id).ToListAsync(ct);
        var chosen = cmd.Ids is { Count: > 0 } ids ? list.Where(p => ids.Contains(p.Id.Value)).ToList() : list;
        var handed = chosen.Count(p => p.HandOver(cmd.SaleId, clock.UtcNow).IsSuccess);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Giao Sale tư vấn lên lớp", cls.Code, $"{handed} học viên");
        return Result.Success(await reader.ToDtosAsync(list, ct));
    }
}
