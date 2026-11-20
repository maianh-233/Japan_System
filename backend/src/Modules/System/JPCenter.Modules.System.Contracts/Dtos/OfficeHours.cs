namespace JPCenter.Modules.System.Contracts.Dtos;

/// <param name="SatTo">null = nghỉ thứ 7</param>
public sealed record OfficeHours(TimeOnly From, TimeOnly To, TimeOnly? SatTo, int LateGraceMin);
