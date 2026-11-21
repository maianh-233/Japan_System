using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.GetAttempts;

/// <summary>CRM-13 — tab Lượt làm / tab Test của lead: lượt làm của lead trong phạm vi, mới gửi trước</summary>
public sealed record GetAttemptsQuery(Guid? TestId, Guid? LeadId, AttemptStatus? Status) : IQuery<IReadOnlyList<PlacementAttemptDto>>;

public sealed class GetAttemptsHandler(ICrmDbContext db, CrmAccess access, PlacementReader reader)
    : IQueryHandler<GetAttemptsQuery, IReadOnlyList<PlacementAttemptDto>>
{
    public async Task<Result<IReadOnlyList<PlacementAttemptDto>>> Handle(GetAttemptsQuery query, CancellationToken ct)
    {
        var leads = reader.VisibleLeads(access, PermissionAction.View);
        var q = db.PlacementAttempts.AsNoTracking().Where(a => leads.Contains(a.LeadId));
        if (query.TestId is { } t) q = q.Where(a => a.TestId == new PlacementTestId(t));
        if (query.LeadId is { } l) q = q.Where(a => a.LeadId == new LeadId(l));
        if (query.Status is { } s) q = q.Where(a => a.Status == s);
        return Result.Success(await reader.AttemptsAsync(await q.OrderByDescending(a => a.SentAt).ToListAsync(ct), ct));
    }
}
