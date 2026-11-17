using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Rooms;

namespace JPCenter.Modules.Academic.Domain.Classes;

/// <summary>Thông tin lớp nhập từ form Mở lớp / Sửa lớp (DT-02, DT-07) — dùng chung cho Create và Update</summary>
/// <param name="Code">Mã đầy đủ gồm tiền tố, vd GVV_N3-K12 (chuẩn hóa chữ hoa)</param>
/// <param name="TeacherIds">1–2 GV — Hr.Employee</param>
/// <param name="AssistantIds">Trợ giảng — Hr.Employee</param>
/// <param name="Weekdays">1 = Thứ 2 … 7 = Chủ nhật</param>
public sealed record ClassSetup(
    string Code,
    string Name,
    BranchId BranchId,
    List<Guid> TeacherIds,
    List<Guid> AssistantIds,
    RoomId? RoomId,
    bool Online,
    string? MeetingUrl,
    DateOnly PlannedStartDate,
    DateOnly PlannedEndDate,
    List<int> Weekdays,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal TotalHours,
    int MinStudents,
    int MaxStudents,
    Money Fee,
    ClassId? PreviousClassId,
    string? Note);
