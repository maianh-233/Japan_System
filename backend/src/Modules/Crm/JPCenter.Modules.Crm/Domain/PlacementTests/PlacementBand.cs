namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>Điểm (%) từ MinPct trở lên → gợi ý trình độ / khóa</summary>
public sealed record PlacementBand(decimal MinPct, string Level, string Course);
