using JPCenter.Modules.System.Application.Integrations.Common;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Integrations;
using JPCenter.SharedKernel.Primitives;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// TH-06 — chọn kênh theo <see cref="ChannelPlan"/>, thử lần lượt, kênh đầu thành công thì dừng. Mỗi lần thử ghi 1 dòng nhật ký của tích hợp
/// (SĐT chỉ 4 số cuối). Thiếu SĐT / email cho kênh → bỏ qua kênh đó (E2). Lỗi xác thực → tích hợp chuyển <c>error</c> rồi thử kênh sau (E3).
/// Không có kênh → ghi nhật ký "Không có kênh gửi" ở tích hợp đầu của việc đó, trả <c>Sent = false</c>.
/// </summary>
internal sealed class MessageChannels(IntegrationCalls calls, ILogger<MessageChannels> logger) : IMessageChannels
{
    public async Task<MessageOutcome> SendAsync(OutgoingMessage message, CancellationToken ct)
    {
        var all = await calls.AllAsync(ct);
        var plan = ChannelPlan.For(message.EventKey, all);
        var byKey = all.ToDictionary(i => i.Key);
        MessageOutcome? outcome = null;
        var notes = new List<string>();

        foreach (var channel in plan)
        {
            var integration = byKey[KeyOf(channel)];
            var address = channel == MessageChannel.Email ? message.Email : message.Phone;
            if (string.IsNullOrWhiteSpace(address))
            {
                var missing = channel == MessageChannel.Email ? "Không có email người nhận" : "Không có SĐT người nhận";
                calls.Note(integration, false, $"{missing} — bỏ qua \"{message.EventKey}\"");
                notes.Add(missing);
                continue;
            }
            var result = await calls.Gateway.SendAsync(channel, calls.ConfigOf(integration), message, ct);
            calls.Record(integration, result);
            if (result.Ok)
            {
                outcome = new MessageOutcome(true, SnakeCase(channel), result.Sandbox, IntegrationCalls.LogText(result));
                break;
            }
            notes.Add(result.Message);
        }

        if (plan.Count == 0)
        {
            notes.Add("Không có kênh gửi");
            if (FirstIntegrationOf(message.EventKey, byKey) is { } first) calls.Note(first, false, $"Không có kênh gửi \"{message.EventKey}\"");
        }
        await calls.SaveAsync(ct);
        if (outcome is null) logger.LogInformation("Không gửi được tin {EventKey}: {Notes}", message.EventKey, string.Join("; ", notes));
        return outcome ?? new MessageOutcome(false, null, false, string.Join("; ", notes));
    }

    private static string KeyOf(MessageChannel channel) => channel switch
    {
        MessageChannel.Zalo => IntegrationKeys.ZaloOa,
        MessageChannel.Sms => IntegrationKeys.Sms,
        _ => IntegrationKeys.Email,
    };

    private static string SnakeCase(MessageChannel channel) => channel.ToString().ToLowerInvariant();

    /// <summary>Tích hợp "chủ" của việc (để ghi nhật ký khi không có kênh)</summary>
    private static Integration? FirstIntegrationOf(string eventKey, IReadOnlyDictionary<string, Integration> byKey) =>
        byKey.Values.FirstOrDefault(i => i.Key is IntegrationKeys.ZaloOa or IntegrationKeys.Sms or IntegrationKeys.Email && i.Spec.Events.Contains(eventKey));
}
