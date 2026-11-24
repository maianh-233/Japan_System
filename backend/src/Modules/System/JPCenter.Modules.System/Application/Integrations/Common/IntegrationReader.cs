using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Integrations.Common;

/// <summary>Dựng <see cref="IntegrationDto"/>: che khóa bí mật, nhãn việc, lượt dùng 30 ngày đếm từ nhật ký (TH-01), tên người kết nối.</summary>
public sealed class IntegrationReader(ISystemDbContext db, IntegrationCalls calls, IEmployeeDirectory employees, IClock clock)
{
    public const int UsageDays = 30;
    public const int LogLimit = 50;

    public async Task<IReadOnlyList<IntegrationDto>> ListAsync(CancellationToken ct)
    {
        var list = await db.Integrations.AsNoTracking().ToListAsync(ct);
        var order = IntegrationCatalog.All.Select(s => s.Key).ToList();
        return await ToDtosAsync(list.OrderBy(i => order.IndexOf(i.Key)).ToList(), ct);
    }

    public async Task<IntegrationDto> ToDtoAsync(Integration integration, CancellationToken ct) => (await ToDtosAsync([integration], ct))[0];

    public async Task<IReadOnlyList<IntegrationLogDto>> LogsAsync(Integration integration, CancellationToken ct) =>
        await db.IntegrationLogs.AsNoTracking()
            .Where(l => l.IntegrationId == integration.Id)
            .OrderByDescending(l => l.At).ThenByDescending(l => l.Id)
            .Take(LogLimit)
            .Select(l => new IntegrationLogDto(l.Id.Value, integration.Key, l.At, l.Ok, l.Message))
            .ToListAsync(ct);

    private async Task<IReadOnlyList<IntegrationDto>> ToDtosAsync(IReadOnlyList<Integration> list, CancellationToken ct)
    {
        var since = clock.UtcNow.AddDays(-UsageDays);
        var ids = list.Select(i => i.Id).ToList();
        var usage = await db.IntegrationLogs.AsNoTracking()
            .Where(l => ids.Contains(l.IntegrationId) && l.At >= since)
            .GroupBy(l => l.IntegrationId)
            .Select(g => new { Id = g.Key, Total = g.Count(), Failed = g.Count(l => !l.Ok) })
            .ToDictionaryAsync(x => x.Id, ct);

        var accountIds = list.Where(i => i.ConnectedBy is { } g && g != Guid.Empty).Select(i => new UserAccountId(i.ConnectedBy!.Value)).Distinct().ToList();
        var accounts = await db.UserAccounts.AsNoTracking().Where(a => accountIds.Contains(a.Id))
            .Select(a => new { a.Id, a.EmployeeId }).ToDictionaryAsync(a => a.Id.Value, a => a.EmployeeId, ct);
        var names = accounts.Count == 0 ? new Dictionary<Guid, string>()
            : (await employees.FindManyAsync(accounts.Values.Distinct().ToList(), ct)).ToDictionary(x => x.Key, x => x.Value.FullName);

        return list.Select(i =>
        {
            var spec = i.Spec;
            var used = usage.GetValueOrDefault(i.Id);
            string? by = i.ConnectedBy is { } a && accounts.TryGetValue(a, out var emp) ? names.GetValueOrDefault(emp) : null;
            return new IntegrationDto(i.Key, i.Name, i.Category, i.Description, i.Status, i.Account, i.ConnectedAt, by, i.LastSyncAt, i.LastError,
                spec.Fields.Select(f => new IntegrationFieldDto(f.Key, f.Label, f.Secret, f.Required)).ToList(),
                i.MaskedConfig(calls.Reveal),
                spec.Events.Select(e => new IntegrationEventDto(e, IntegrationTexts.EventLabel(i.Key, e), i.Events.GetValueOrDefault(e))).ToList(),
                new IntegrationUsageDto(IntegrationTexts.UsageLabel(i.Key), used?.Total ?? 0, used?.Failed ?? 0),
                i.WebhookUrl);
        }).ToList();
    }
}
