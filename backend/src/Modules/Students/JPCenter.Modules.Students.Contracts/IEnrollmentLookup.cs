using JPCenter.Modules.Students.Contracts.Dtos;

namespace JPCenter.Modules.Students.Contracts;

/// <summary>Tra cứu ghi danh — Academic tính sĩ số lớp (quy tắc vàng 1, user story 07), lấy danh sách lớp để điểm danh.</summary>
public interface IEnrollmentLookup
{
    /// <summary>Số ghi danh HIỆU LỰC (đang học + bảo lưu) theo lớp; lớp không có ai → 0</summary>
    Task<IReadOnlyDictionary<Guid, int>> ActiveCountByClassAsync(IReadOnlyCollection<Guid> classIds, CancellationToken ct);

    /// <summary>Học viên có ghi danh hiệu lực trong lớp (kèm bảo lưu), sắp theo tên</summary>
    Task<IReadOnlyList<RosterEntry>> RosterAsync(Guid classId, CancellationToken ct);

    /// <summary>Mọi ghi danh của 1 HV (cổng HV / PH: lớp của tôi, kiểm HV có trong lớp khi quét QR / xin nghỉ)</summary>
    Task<IReadOnlyList<StudentEnrollmentEntry>> OfStudentAsync(Guid studentId, CancellationToken ct);

    /// <summary>Họ tên HV theo Id (danh sách nhận xét / lên lớp)</summary>
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken ct);

    /// <summary>Hồ sơ rút gọn của các học viên (kể cả đã ẩn) — cổng PH hiển thị danh sách con</summary>
    Task<IReadOnlyList<StudentSummary>> SummariesAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken ct);
}
