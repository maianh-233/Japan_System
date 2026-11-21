using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Messages.Common;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Messages.SaveMessageTemplate;

/// <summary>CRM-17 — thêm (<paramref name="Id"/> null) / sửa mẫu tin, bật / tắt; nội dung trống → E1</summary>
public sealed record SaveMessageTemplateCommand(Guid? Id, MessageScenario Scenario, string? Name, ContactChannel Channel, string? Body, bool Active,
    uint Version) : ICommand<MessageTemplateDto>;

public sealed class SaveMessageTemplateHandler(ICrmDbContext db, TemplateReader reader) : ICommandHandler<SaveMessageTemplateCommand, MessageTemplateDto>
{
    public async Task<Result<MessageTemplateDto>> Handle(SaveMessageTemplateCommand cmd, CancellationToken ct)
    {
        MessageTemplate t;
        if (cmd.Id is { } id)
        {
            var found = await db.MessageTemplates.FirstOrDefaultAsync(x => x.Id == new MessageTemplateId(id), ct);
            if (found is null) return MessageErrors.TemplateNotFound;
            if (cmd.Version != 0) ((DbContext)db).Entry(found).Property("xmin").OriginalValue = cmd.Version;
            if (found.Update(cmd.Scenario, cmd.Name ?? "", cmd.Channel, cmd.Body ?? "") is { IsFailure: true } bad) return bad.Error;
            t = found;
        }
        else
        {
            var created = MessageTemplate.Create(cmd.Scenario, cmd.Name ?? "", cmd.Channel, cmd.Body ?? "");
            if (created.IsFailure) return created.Error;
            t = created.Value;
            db.MessageTemplates.Add(t);
        }
        t.SetActive(cmd.Active);
        await db.SaveChangesAsync(ct);
        return (await reader.ToDtosAsync([t], ct))[0];
    }
}
