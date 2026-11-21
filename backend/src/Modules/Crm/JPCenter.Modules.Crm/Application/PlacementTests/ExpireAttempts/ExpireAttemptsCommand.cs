using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.ExpireAttempts;

/// <summary>
/// CRM-13.E7 — job hằng giờ: lượt Đã gửi / Đang làm quá hạn → Hết hạn; đang làm thì chấm phần đã lưu (ghi lượt làm vào thống kê câu).
/// Trả số lượt đã chuyển.
/// </summary>
public sealed record ExpireAttemptsCommand : ICommand<int>;

public sealed class ExpireAttemptsHandler(ICrmDbContext db, PlacementBank bank, IClock clock) : ICommandHandler<ExpireAttemptsCommand, int>
{
    public async Task<Result<int>> Handle(ExpireAttemptsCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.PlacementAttempts
            .Where(a => (a.Status == AttemptStatus.Sent || a.Status == AttemptStatus.InProgress) && a.ExpiresAt < now).ToListAsync(ct);
        if (due.Count == 0) return 0;
        var testIds = due.Select(a => a.TestId).Distinct().ToList();
        var tests = await db.PlacementTests.IgnoreQueryFilters().Where(t => testIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);

        var changed = 0;
        foreach (var a in due)
        {
            (PlacementScore, PlacementBand?) graded = (new PlacementScore(0, 0, []), null);
            if (a.Status == AttemptStatus.InProgress && tests.TryGetValue(a.TestId, out var test))
                graded = await bank.GradeAsync(test, a.Answers, record: true, ct);
            if (a.ExpireIfDue(now, _ => graded)) changed++;
        }
        await db.SaveChangesAsync(ct);
        return changed;
    }
}
