namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>1 phần thi JLPT: điểm tối đa + điểm liệt</summary>
public sealed record JlptSection(string Name, int Max, int Min);
