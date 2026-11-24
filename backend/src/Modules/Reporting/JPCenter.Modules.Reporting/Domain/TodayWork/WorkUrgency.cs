namespace JPCenter.Modules.Reporting.Domain.TodayWork;

/// <summary>Mức độ + nhãn ngắn bên phải của 1 việc (vd "quá 2 giờ", "hạn hôm nay")</summary>
public sealed record WorkUrgency(WorkTone Tone, string Meta);
