using JPCenter.Modules.Academic.Contracts.Dtos;

namespace JPCenter.Modules.Academic.Contracts;

/// <summary>Chuyên cần theo HV × lớp — Students tính nguy cơ bỏ học (HV-14), chứng nhận (HV-15).</summary>
public interface IAttendanceStats
{
    /// <summary>
    /// Số liệu chuyên cần của HV trong các lớp (chỉ buổi chưa hủy đã điểm danh, không tính khách học bù).
    /// <paramref name="studentIds"/> null = mọi HV có điểm danh trong lớp.
    /// </summary>
    Task<IReadOnlyList<AttendanceStat>> ForClassesAsync(IReadOnlyCollection<Guid> classIds, IReadOnlyCollection<Guid>? studentIds, CancellationToken ct);
}
