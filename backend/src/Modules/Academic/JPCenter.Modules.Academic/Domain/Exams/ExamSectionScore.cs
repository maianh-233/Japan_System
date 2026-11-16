namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>Điểm 1 phần thi (từ vựng, đọc hiểu, nghe...)</summary>
public sealed record ExamSectionScore(string Name, decimal Score, decimal Max);
