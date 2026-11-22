using JPCenter.Modules.Academic.Contracts.Dtos;

namespace JPCenter.Modules.Academic.Contracts;

/// <summary>
/// Bài tập + kết quả thi theo HV × lớp — Students tính dấu hiệu nguy cơ bỏ học (HV-14, user story 12 quy tắc vàng 4).
/// Ngưỡng "thi kém" do Students áp (Cài đặt chung <c>atRisk.scorePct</c>).
/// </summary>
public interface ILearningStats
{
    /// <summary>Số liệu của HV trong các lớp. <paramref name="studentIds"/> null = mọi HV có dữ liệu trong lớp.</summary>
    Task<IReadOnlyList<LearningStat>> ForClassesAsync(IReadOnlyCollection<Guid> classIds, IReadOnlyCollection<Guid>? studentIds, CancellationToken ct);
}
