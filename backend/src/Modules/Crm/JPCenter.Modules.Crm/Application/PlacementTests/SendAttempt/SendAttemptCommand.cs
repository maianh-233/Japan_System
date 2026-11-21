using System.Security.Cryptography;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.PlacementTests.SendAttempt;

/// <summary>
/// CRM-13 bước 1 — gửi link test (bài đang dùng) cho lead trong phạm vi: token 256 bit ngẫu nhiên (E6), hạn 1 → 30 ngày, ghi tin đã gửi.
/// ⏳ Gửi Zalo / SMS / email thật khi có module Tích hợp (giai đoạn 22) — tin ghi trạng thái "chưa gửi được".
/// </summary>
public sealed record SendAttemptCommand(Guid TestId, Guid LeadId, ContactChannel Channel, int ValidDays) : ICommand<PlacementAttemptDto>;

public sealed class SendAttemptHandler(ICrmDbContext db, CrmAccess access, PlacementReader reader, IClock clock)
    : ICommandHandler<SendAttemptCommand, PlacementAttemptDto>
{
    public async Task<Result<PlacementAttemptDto>> Handle(SendAttemptCommand cmd, CancellationToken ct)
    {
        var lead = await access.Scope(db.Leads, PermissionAction.View, CrmAccess.PlacementTests).FirstOrDefaultAsync(l => l.Id == new LeadId(cmd.LeadId), ct);
        if (lead is null) return LeadErrors.NotFound;
        var test = await db.PlacementTests.FirstOrDefaultAsync(t => t.Id == new PlacementTestId(cmd.TestId), ct);
        if (test is null) return PlacementTestErrors.NotFound;

        var now = clock.UtcNow;
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var sent = PlacementAttempt.Send(test, lead.Id, token, cmd.Channel, access.ActorId, now, cmd.ValidDays);
        if (sent.IsFailure) return sent.Error;
        db.PlacementAttempts.Add(sent.Value);

        var to = cmd.Channel == ContactChannel.Email ? lead.Email ?? lead.Phone : lead.Phone;
        var body = $"Chào {lead.FullName}, JP Center gửi bạn bài test xếp lớp \"{test.Name}\" ({test.DurationMinutes} phút): "
            + $"{reader.LinkOf(token)} — link dùng được đến {sent.Value.ExpiresAt.ToOffset(TimeSpan.FromHours(7)):dd/MM/yyyy HH:mm}.";
        var log = MessageLog.Create(cmd.Channel, MessageScenario.TestReminder, null, lead.FullName, to, lead.Id, null, body, access.ActorId, now);
        if (log.IsSuccess) db.MessageLogs.Add(log.Value);
        lead.AddSystemNote($"Gửi link test \"{test.Name}\" qua {cmd.Channel.ToString().ToLowerInvariant()} (hạn {cmd.ValidDays} ngày)", now);

        await db.SaveChangesAsync(ct);
        return await reader.AttemptAsync(sent.Value, ct);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
