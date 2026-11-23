namespace JPCenter.Modules.Hr.Domain.TimeRecords;

/// <summary>Giờ phải làm trong ngày: giờ hành chính ("HC") hoặc ca được xếp ("S", "C+T"...)</summary>
public sealed record WorkWindow(TimeOnly From, TimeOnly To, string Label);
