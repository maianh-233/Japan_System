namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>Kết quả chấm theo quy chế: tổng điểm, đỗ / trượt, các phần bị điểm liệt</summary>
public sealed record JlptResult(int Total, bool Passed, IReadOnlyList<string> FailSections);
