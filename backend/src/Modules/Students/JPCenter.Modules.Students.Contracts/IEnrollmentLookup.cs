using JPCenter.Modules.Students.Contracts.Dtos;

namespace JPCenter.Modules.Students.Contracts;

/// <summary>Tra cứu ghi danh — Academic tính sĩ số lớp (quy tắc vàng 1, user story 07), lấy danh sách lớp để điểm danh.</summary>
public interface IEnrollmentLookup
{
    /// <summary>Số ghi danh HIỆU LỰC (đang học + bảo lưu) theo lớp; lớp không có ai → 0</summary>
    Task<IReadOnlyDictionary<Guid, int>> ActiveCountByClassAsync(IReadOnlyCollection<Guid> classIds, CancellationToken ct);

    /// <summary>Học viên có ghi danh hiệu lực trong lớp (kèm bảo lưu), sắp theo tên</summary>
    Task<IReadOnlyList<RosterEntry>> RosterAsync(Guid classId, CancellationToken ct);
}
