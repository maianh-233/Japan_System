using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Promotions.Common;

/// <summary>
/// Phía Sale của lên lớp (CRM-19) — quyền trang <c>crm.promotions</c>: đề xuất ĐÃ GIAO SALE trong chi nhánh được gán,
/// phạm vi own = đề xuất giao cho mình.
/// </summary>
public sealed class SalePromotions(IAcademicDbContext db, AcademicAccess access)
{
    public const string Page = "crm.promotions";

    public IQueryable<PromotionCandidate> Scope(IQueryable<PromotionCandidate> q, PermissionAction action)
    {
        q = q.Where(p => p.HandedOverAt != null);
        if (access.Branches(Page, action) is { } allowed)
        {
            var ids = allowed.Select(b => new BranchId(b)).ToList();
            q = q.Where(p => ids.Contains(p.BranchId));
        }
        if (access.OwnOnly(Page, action) is { } me) q = q.Where(p => p.SaleId == me);
        return q;
    }

    public Task<PromotionCandidate?> ForUpdateAsync(Guid id, CancellationToken ct) =>
        Scope(db.PromotionCandidates, PermissionAction.Update).FirstOrDefaultAsync(p => p.Id == new PromotionCandidateId(id), ct);
}
