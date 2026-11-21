using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Messages.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Messages.SendMessage;

/// <summary>
/// CRM-17 bước 2 — gửi tin cho lead / HV / PH: nội dung thực tế (đã điền biến, người gửi có thể sửa) ghi vào nhật ký tin.
/// Lead phải trong phạm vi xem. ⏳ Chưa có tích hợp Zalo OA / SMS (giai đoạn 22) → trạng thái "chưa gửi được" (E4).
/// </summary>
public sealed record SendMessageCommand(ContactChannel Channel, MessageScenario Scenario, Guid? TemplateId, Guid? LeadId, Guid? StudentId,
    string? ToName, string? ToPhone, string? Body) : ICommand<MessageLogDto>;

public sealed class SendMessageHandler(ICrmDbContext db, CrmAccess access, IClock clock) : ICommandHandler<SendMessageCommand, MessageLogDto>
{
    public async Task<Result<MessageLogDto>> Handle(SendMessageCommand cmd, CancellationToken ct)
    {
        Lead? lead = null;
        if (cmd.LeadId is { } lid)
        {
            lead = await access.Scope(db.Leads.AsNoTracking(), PermissionAction.View).FirstOrDefaultAsync(l => l.Id == new LeadId(lid), ct);
            if (lead is null) return LeadErrors.NotFound;
        }
        var toName = string.IsNullOrWhiteSpace(cmd.ToName) ? lead?.FullName ?? "" : cmd.ToName;
        var to = string.IsNullOrWhiteSpace(cmd.ToPhone)
            ? cmd.Channel == ContactChannel.Email ? lead?.Email : lead?.Phone
            : cmd.ToPhone;
        var log = MessageLog.Create(cmd.Channel, cmd.Scenario, cmd.TemplateId is { } t ? new MessageTemplateId(t) : null, toName, to ?? "",
            lead?.Id, cmd.StudentId, cmd.Body ?? "", access.ActorId, clock.UtcNow);
        if (log.IsFailure) return log.Error;
        db.MessageLogs.Add(log.Value);
        await db.SaveChangesAsync(ct);
        var m = log.Value;
        return new MessageLogDto(m.Id.Value, m.Channel, m.Scenario, m.TemplateId?.Value, m.ToName, m.ToPhone, m.LeadId?.Value, m.StudentId, m.Body,
            m.SentBy, access.ActorName, m.SentAt, m.Auto, m.Status);
    }
}
