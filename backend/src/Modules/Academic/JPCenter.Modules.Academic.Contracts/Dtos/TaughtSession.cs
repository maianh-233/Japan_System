namespace JPCenter.Modules.Academic.Contracts.Dtos;

/// <summary>
/// 1 người đứng lớp trong 1 buổi — Finance tính lương GV / trợ giảng (TC-13). Buổi hủy: <paramref name="Cancelled"/> = true, phút 0 (chỉ để đếm).
/// Buổi chưa hủy chỉ trả người có check-in + check-out và không vắng.
/// </summary>
/// <param name="BranchId">Chi nhánh của LỚP — buổi tính vào chi nhánh lớp (TC-13.E5)</param>
/// <param name="TeacherId">Hr.Employee</param>
/// <param name="Role">"teacher" · "substitute" · "assistant"</param>
/// <param name="CheckIn">Giờ vào lớp (giờ VN); null với buổi hủy</param>
/// <param name="Minutes">Phút thực dạy = check-out − check-in (TC-13.E6)</param>
/// <param name="Late">GV đến muộn</param>
/// <param name="Confirmed">Giáo vụ đã xác nhận buổi (DT-15)</param>
public sealed record TaughtSession(
    Guid SessionId,
    Guid ClassId,
    string ClassCode,
    string ClassName,
    Guid BranchId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid TeacherId,
    string Role,
    bool Online,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    int Minutes,
    bool Late,
    bool Confirmed,
    bool Cancelled);
