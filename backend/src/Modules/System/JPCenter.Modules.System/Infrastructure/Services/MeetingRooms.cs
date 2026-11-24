using JPCenter.Modules.System.Application.Integrations.Common;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using JPCenter.Modules.System.Domain.Integrations;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>
/// TH-08 — Zoom (đã kết nối, không lỗi, việc <c>autoRoom</c> bật) → lỗi / không dùng được thì Google Meet → không có → link trống.
/// Mỗi lần thử ghi nhật ký tích hợp tương ứng.
/// </summary>
internal sealed class MeetingRooms(IntegrationCalls calls) : IMeetingRooms
{
    public const string AutoRoom = "autoRoom";

    public async Task<MeetingRoom> CreateAsync(Guid classId, string topic, CancellationToken ct)
    {
        var all = (await calls.AllAsync(ct)).ToDictionary(i => i.Key);
        var notes = new List<string>();
        foreach (var key in new[] { IntegrationKeys.Zoom, IntegrationKeys.Meet })
        {
            if (!all.TryGetValue(key, out var integration) || !integration.IsActive(AutoRoom)) continue;
            var result = await calls.Gateway.CreateRoomAsync(key, calls.ConfigOf(integration), topic, ct);
            calls.Record(integration, result);
            if (result.Ok && !string.IsNullOrEmpty(result.Value))
            {
                await calls.SaveAsync(ct);
                return new MeetingRoom(result.Value, key, IntegrationCalls.LogText(result));
            }
            notes.Add(result.Message);
        }
        await calls.SaveAsync(ct);
        return new MeetingRoom(null, null, notes.Count == 0 ? "Chưa bật tự tạo phòng (Zoom / Google Meet)" : string.Join("; ", notes));
    }
}
