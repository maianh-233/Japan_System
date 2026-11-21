using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.LogAttempt;

/// <summary>CRM-13 bước 4 — "Ghi vào lịch sử tư vấn": dòng hệ thống kết quả + khóa quan tâm = khóa gợi ý, đánh dấu đã ghi (1 lần)</summary>
public sealed record LogAttemptCommand(Guid Id) : ICommand<PlacementAttemptDto>;

public sealed class LogAttemptHandler(ICrmDbContext db, CrmAccess access, PlacementReader reader, IClock clock)
    : ICommandHandler<LogAttemptCommand, PlacementAttemptDto>
{
    public async Task<Result<PlacementAttemptDto>> Handle(LogAttemptCommand cmd, CancellationToken ct)
    {
        var attempt = await db.PlacementAttempts.FirstOrDefaultAsync(a => a.Id == new PlacementAttemptId(cmd.Id), ct);
        if (attempt is null) return PlacementAttemptErrors.NotFound;
        var lead = await access.Scope(db.Leads, PermissionAction.View, CrmAccess.PlacementTests).FirstOrDefaultAsync(l => l.Id == attempt.LeadId, ct);
        if (lead is null) return PlacementAttemptErrors.NotFound;
        var testName = await db.PlacementTests.IgnoreQueryFilters().Where(t => t.Id == attempt.TestId).Select(t => t.Name).FirstAsync(ct);

        if (attempt.MarkLogged() is { IsFailure: true } bad) return bad.Error;
        var total = attempt.Total ?? 0;
        lead.RecordPlacementResult(testName, attempt.Correct ?? 0, total, total == 0 ? 0 : Math.Round((attempt.Correct ?? 0) * 100m / total, 1),
            attempt.SuggestedLevel, attempt.SuggestedCourse, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await reader.AttemptAsync(attempt, ct);
    }
}
