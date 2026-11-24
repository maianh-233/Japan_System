namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>
/// TH-06 — thứ tự kênh thử gửi cho 1 việc. Gửi lần lượt, kênh đầu thành công thì dừng.
/// <list type="bullet">
/// <item>OTP (<c>otp</c>) → chỉ SMS.</item>
/// <item>Việc email (<c>invoice</c>, <c>invite</c>, <c>test</c>) → chỉ email, không dự phòng chéo.</item>
/// <item>Việc Zalo (<c>attendance</c>, <c>tuition</c>, <c>schedule</c>, <c>result</c>): Zalo nếu đang chạy + việc bật; SMS dự phòng (việc <c>fallback</c> bật)
/// khi Zalo gửi lỗi hoặc Zalo không chạy (chưa kết nối / lỗi). Admin đã TẮT việc trên Zalo đang chạy = không muốn gửi tin này → không gửi SMS thay.</item>
/// </list>
/// Danh sách rỗng = không có kênh → ghi nhật ký, không lỗi nghiệp vụ.
/// </summary>
public static class ChannelPlan
{
    public const string Otp = "otp";
    public const string Fallback = "fallback";
    private static readonly string[] EmailEvents = ["invoice", "invite", "test"];

    public static IReadOnlyList<MessageChannel> For(string eventKey, IEnumerable<Integration> integrations)
    {
        var byKey = integrations.ToDictionary(i => i.Key);
        bool Active(string key, string evt) => byKey.TryGetValue(key, out var i) && i.IsActive(evt);

        if (eventKey == Otp) return Active(IntegrationKeys.Sms, Otp) ? [MessageChannel.Sms] : [];
        if (EmailEvents.Contains(eventKey)) return Active(IntegrationKeys.Email, eventKey) ? [MessageChannel.Email] : [];

        var zalo = byKey.GetValueOrDefault(IntegrationKeys.ZaloOa);
        if (zalo is null || !zalo.Spec.Events.Contains(eventKey)) return [];
        var zaloRunning = zalo.Status == IntegrationStatus.Connected;
        if (zaloRunning && !zalo.Events.GetValueOrDefault(eventKey)) return [];

        var plan = new List<MessageChannel>(2);
        if (zaloRunning) plan.Add(MessageChannel.Zalo);
        if (Active(IntegrationKeys.Sms, Fallback)) plan.Add(MessageChannel.Sms);
        return plan;
    }
}
