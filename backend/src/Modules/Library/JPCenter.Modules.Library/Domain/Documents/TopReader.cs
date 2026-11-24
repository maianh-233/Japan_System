namespace JPCenter.Modules.Library.Domain.Documents;

/// <summary>1 người trong top đọc nhiều nhất</summary>
public sealed record TopReader(string Name, string Role, int Views);
