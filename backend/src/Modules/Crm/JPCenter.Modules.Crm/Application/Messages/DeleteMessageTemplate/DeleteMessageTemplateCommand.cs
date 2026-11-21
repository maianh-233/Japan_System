using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Messages.DeleteMessageTemplate;

/// <summary>Xóa mềm mẫu tin (tin đã gửi giữ nguyên nội dung)</summary>
public sealed record DeleteMessageTemplateCommand(Guid Id) : ICommand;

public sealed class DeleteMessageTemplateHandler(ICrmDbContext db, IAuditTrail audit) : ICommandHandler<DeleteMessageTemplateCommand>
{
    public async Task<Result> Handle(DeleteMessageTemplateCommand cmd, CancellationToken ct)
    {
        var t = await db.MessageTemplates.FirstOrDefaultAsync(x => x.Id == new MessageTemplateId(cmd.Id), ct);
        if (t is null) return MessageErrors.TemplateNotFound;
        db.MessageTemplates.Remove(t);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Xóa mẫu tin nhắn", t.Name);
        return Result.Success();
    }
}
