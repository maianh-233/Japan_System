namespace JPCenter.Modules.Academic.Contracts.Dtos;

/// <summary>Số liệu học tập 1 HV trong 1 lớp (HV-14)</summary>
/// <param name="Due">Bài tập đã quá hạn nộp (chưa xóa)</param>
/// <param name="Submitted">Số bài đã nộp trong các bài quá hạn</param>
/// <param name="AssignmentAvgPct">Điểm bài tập trung bình (% thang điểm) trên các bài đã có điểm — null = chưa có điểm</param>
/// <param name="ExamPercents">Mỗi kết quả thi 1 phần tử: % điểm, null = vắng thi</param>
public sealed record LearningStat(Guid ClassId, Guid StudentId, int Due, int Submitted, int? AssignmentAvgPct, IReadOnlyList<int?> ExamPercents);
