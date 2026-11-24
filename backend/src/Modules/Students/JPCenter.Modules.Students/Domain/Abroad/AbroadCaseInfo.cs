using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Abroad;

/// <summary>Thông tin chung hồ sơ du học / XKLĐ (HV-17 bước 1) — tạo / sửa</summary>
/// <param name="Intake">Kỳ nhập học / xuất cảnh dự kiến, dạng YYYY-MM</param>
/// <param name="AssigneeId">Người phụ trách (Hr.Employee)</param>
public sealed record AbroadCaseInfo(
    AbroadProgram Program,
    string? Partner,
    string? Destination,
    string? Job,
    string? Intake,
    JlptLevel? JlptRequired,
    Money FeeTotal,
    Guid AssigneeId);
