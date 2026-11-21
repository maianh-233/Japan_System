using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Messages.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Messages.GetMessageTemplates;

/// <summary>CRM-17 bước 1 — danh sách mẫu tin theo tình huống</summary>
public sealed record GetMessageTemplatesQuery : IQuery<IReadOnlyList<MessageTemplateDto>>;

public sealed class GetMessageTemplatesHandler(ICrmDbContext db, TemplateReader reader) : IQueryHandler<GetMessageTemplatesQuery, IReadOnlyList<MessageTemplateDto>>
{
    public async Task<Result<IReadOnlyList<MessageTemplateDto>>> Handle(GetMessageTemplatesQuery query, CancellationToken ct) =>
        Result.Success(await reader.ToDtosAsync(await db.MessageTemplates.AsNoTracking().OrderBy(t => t.Scenario).ThenBy(t => t.Name).ToListAsync(ct), ct));
}
