using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Messages.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Messages.GetMessages;

/// <summary>CRM-17 bước 3 — tin đã gửi cho 1 lead (tab Tin đã gửi — lead trong phạm vi) hoặc 1 HV, mới nhất trước</summary>
public sealed record GetMessagesQuery(Guid? LeadId, Guid? StudentId) : IQuery<IReadOnlyList<MessageLogDto>>;

public sealed class GetMessagesHandler(ICrmDbContext db, CrmAccess access, PeopleNames people) : IQueryHandler<GetMessagesQuery, IReadOnlyList<MessageLogDto>>
{
    public static readonly Error TargetRequired = Error.Validation("crm.message.target_required", "Chọn lead hoặc học viên");

    public async Task<Result<IReadOnlyList<MessageLogDto>>> Handle(GetMessagesQuery query, CancellationToken ct)
    {
        var q = db.MessageLogs.AsNoTracking();
        if (query.LeadId is { } lid)
        {
            var visible = await access.Scope(db.Leads.AsNoTracking().IgnoreQueryFilters(), PermissionAction.View).AnyAsync(l => l.Id == new LeadId(lid), ct);
            if (!visible) return LeadErrors.NotFound;
            q = q.Where(m => m.LeadId == new LeadId(lid));
        }
        else if (query.StudentId is { } sid) q = q.Where(m => m.StudentId == sid);
        else return TargetRequired;

        var list = await q.OrderByDescending(m => m.SentAt).Take(200).ToListAsync(ct);
        var names = await people.OfAsync(list.Select(m => m.SentBy), ct);
        return list.Select(m => new MessageLogDto(m.Id.Value, m.Channel, m.Scenario, m.TemplateId?.Value, m.ToName, m.ToPhone, m.LeadId?.Value,
            m.StudentId, m.Body, m.SentBy, m.SentBy is { } by ? names.GetValueOrDefault(by) ?? "—" : "Hệ thống", m.SentAt, m.Auto, m.Status)).ToList();
    }
}
