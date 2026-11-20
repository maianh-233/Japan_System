namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>1 ngày nghỉ lễ của trung tâm (Cài đặt chung — khóa <c>academic.holidays</c>).</summary>
public sealed record Holiday(DateOnly Date, string Name);
