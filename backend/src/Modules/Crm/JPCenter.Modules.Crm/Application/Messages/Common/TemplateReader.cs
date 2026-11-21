using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Domain.Messages;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Messages.Common;

/// <summary>Dựng DTO mẫu tin (lần sửa cuối + phiên bản)</summary>
public sealed class TemplateReader(ICrmDbContext db)
{
    public async Task<IReadOnlyList<MessageTemplateDto>> ToDtosAsync(IReadOnlyList<MessageTemplate> list, CancellationToken ct)
    {
        var ids = list.Select(t => t.Id).ToList();
        var meta = await db.MessageTemplates.AsNoTracking().Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, At = EF.Property<DateTimeOffset?>(t, "UpdatedAt") ?? EF.Property<DateTimeOffset>(t, "CreatedAt"),
                Version = EF.Property<uint>(t, "xmin") })
            .ToDictionaryAsync(x => x.Id, ct);
        return list.Select(t => new MessageTemplateDto(t.Id.Value, t.Scenario, t.Name, t.Channel, t.Body, t.Active,
            meta.GetValueOrDefault(t.Id)?.At ?? default, meta.GetValueOrDefault(t.Id)?.Version ?? 0)).ToList();
    }
}
