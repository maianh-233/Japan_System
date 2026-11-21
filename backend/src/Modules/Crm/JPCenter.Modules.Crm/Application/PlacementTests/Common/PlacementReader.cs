using JPCenter.Application.Common.Email;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.Common;

/// <summary>Dựng DTO bài test / lượt làm (tên người soạn, người gửi, lead; link làm bài)</summary>
public sealed class PlacementReader(ICrmDbContext db, PeopleNames people, IAppLinks links)
{
    public string LinkOf(string token) => links.Page($"/t/{token}");

    public async Task<IReadOnlyList<PlacementTestDto>> TestsAsync(IReadOnlyList<PlacementTest> tests, CancellationToken ct)
    {
        if (tests.Count == 0) return [];
        var ids = tests.Select(t => t.Id).ToList();
        var meta = await db.PlacementTests.AsNoTracking().IgnoreQueryFilters().Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, Created = EF.Property<DateTimeOffset>(t, "CreatedAt"), Version = EF.Property<uint>(t, "xmin") })
            .ToDictionaryAsync(x => x.Id, ct);
        var attempts = await db.PlacementAttempts.AsNoTracking().Where(a => ids.Contains(a.TestId)).GroupBy(a => a.TestId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var names = await people.OfAsync(tests.Select(t => (Guid?)t.AuthorId), ct);
        return tests.Select(t => new PlacementTestDto(t.Id.Value, t.Code, t.Name, t.Description,
            t.Sections.Select(s => new SectionDto(PlacementBank.LevelText(s.Level), s.Skill, s.Count)).ToList(), t.QuestionIds, t.DurationMinutes,
            t.Bands.Select(b => new BandDto(b.MinPct, b.Level, b.Course)).ToList(), t.Status, t.AuthorId, names.GetValueOrDefault(t.AuthorId) ?? "—",
            meta.TryGetValue(t.Id, out var m) ? m.Created : default, attempts.GetValueOrDefault(t.Id), meta.TryGetValue(t.Id, out var v) ? v.Version : 0))
            .ToList();
    }

    public async Task<PlacementTestDto> TestAsync(PlacementTest test, CancellationToken ct) => (await TestsAsync([test], ct))[0];

    public async Task<IReadOnlyList<PlacementAttemptDto>> AttemptsAsync(IReadOnlyList<PlacementAttempt> attempts, CancellationToken ct)
    {
        if (attempts.Count == 0) return [];
        var testIds = attempts.Select(a => a.TestId).Distinct().ToList();
        var leadIds = attempts.Select(a => a.LeadId).Distinct().ToList();
        var tests = await db.PlacementTests.AsNoTracking().IgnoreQueryFilters().Where(t => testIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        var leads = await db.Leads.AsNoTracking().IgnoreQueryFilters().Where(l => leadIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.FullName, ct);
        var names = await people.OfAsync(attempts.Select(a => (Guid?)a.SentBy), ct);
        return attempts.Select(a => new PlacementAttemptDto(a.Id.Value, a.TestId.Value, tests.GetValueOrDefault(a.TestId) ?? "—", a.LeadId.Value,
            leads.GetValueOrDefault(a.LeadId) ?? "—", a.Token, LinkOf(a.Token), a.Channel, a.SentBy, names.GetValueOrDefault(a.SentBy) ?? "—",
            a.SentAt, a.ExpiresAt, a.Status, a.StartedAt, a.SubmittedAt, a.Answers, a.Correct, a.Total,
            a.Total is > 0 ? Math.Round((a.Correct ?? 0) * 100m / a.Total.Value, 1) : null, a.BySkill, a.SuggestedLevel, a.SuggestedCourse, a.Logged))
            .ToList();
    }

    public async Task<PlacementAttemptDto> AttemptAsync(PlacementAttempt a, CancellationToken ct) => (await AttemptsAsync([a], ct))[0];

    /// <summary>Lead được xem theo phạm vi trang (own = lead mình phụ trách)</summary>
    public IQueryable<LeadId> VisibleLeads(CrmAccess access, PermissionAction action) =>
        access.Scope(db.Leads.AsNoTracking().IgnoreQueryFilters(), action, CrmAccess.PlacementTests).Select(l => l.Id);
}
