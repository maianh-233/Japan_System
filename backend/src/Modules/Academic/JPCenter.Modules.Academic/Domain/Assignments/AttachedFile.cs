namespace JPCenter.Modules.Academic.Domain.Assignments;

/// <summary>Tệp đính kèm / bài nộp (jsonb). Url = khóa trong kho file hoặc link ngoài.</summary>
public sealed record AttachedFile(string Name, FileKind Kind, string Url, long? Size);
