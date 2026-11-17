using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Employees.Common;

/// <summary>
/// Hồ sơ nhân sự — khớp <c>Employee</c> ở <c>frontend/src/features/hr/types.ts</c> (+ tên quản lý, lý do / hẹn nghỉ việc, version...).
/// Enum trả dạng snake_case (staff, part_time, morning...) giống frontend; cấp độ dạy giữ dạng "N5".
/// </summary>
/// <param name="SalaryHidden">true = người xem không được xem lương (NS-05.E3) — lương / phụ cấp / đơn giá để trống</param>
/// <param name="ContractEndsIn">Số ngày còn lại của hợp đồng (âm = đã hết hạn); null = không thời hạn</param>
/// <param name="QuitScheduled">Đã hẹn ngày nghỉ (chưa tới ngày — NS-07.E6)</param>
/// <param name="ContractHistory">Hợp đồng trước đây — chỉ có khi xem chi tiết</param>
public sealed record EmployeeDto(
    Guid Id,
    string Code,
    string FullName,
    Gender Gender,
    DateOnly? Dob,
    string Phone,
    string Email,
    Guid BranchId,
    EmployeeKind Kind,
    Department Department,
    string Position,
    bool ShiftBased,
    EmployeeStatus Status,
    DateOnly JoinedAt,
    DateOnly? QuitAt,
    string? QuitReason,
    bool QuitScheduled,
    Guid? ManagerId,
    string? ManagerName,
    ContractDto Contract,
    int? ContractEndsIn,
    decimal LeaveBalance,
    bool HasAccount,
    TeacherDto? Teacher,
    string? Note,
    bool SalaryHidden,
    uint Version,
    IReadOnlyList<ContractDto>? ContractHistory);

public sealed record ContractDto(ContractType Type, string No, DateOnly Start, DateOnly? End, decimal? BaseSalary, decimal? Allowance);

/// <param name="Type">"vn" GV Việt · "jp" GV người Nhật</param>
/// <param name="Levels">"N5"…"N1"</param>
/// <param name="Rate">null khi bị ẩn lương</param>
/// <param name="Availability">Thứ (1 = T2 … 7 = CN) → ca</param>
public sealed record TeacherDto(
    TeacherNationality Type,
    IReadOnlyList<string> Levels,
    IReadOnlyList<string> Certificates,
    RateDto? Rate,
    IReadOnlyDictionary<int, List<ShiftKey>> Availability,
    IReadOnlyList<Guid> ExtraBranchIds,
    string? Bio);

public sealed record RateDto(decimal Offline, decimal Online);

/// <summary>Danh sách + số liệu cho thẻ đầu trang và chip nhóm (NS-01, NS-02)</summary>
/// <param name="Groups">Số người theo nhóm (phòng ban / vn · jp · assistant) — theo bộ lọc trừ nhóm</param>
public sealed record EmployeeListResult(
    IReadOnlyList<EmployeeDto> Items, int Total, int Page, int PageSize, EmployeeStats Stats, IReadOnlyDictionary<string, int> Groups);

/// <summary>Không tính người đã nghỉ</summary>
/// <param name="ContractEnding">Hợp đồng hết hạn trong ≤ 45 ngày (kể cả đã quá hạn) — NS-09</param>
public sealed record EmployeeStats(int Active, int Probation, int ContractEnding, int NoAccount);

/// <summary>Kết quả thao tác kèm cảnh báo không chặn (vd đang quản lý người khác — NS-07.E3)</summary>
public sealed record EmployeeResult(EmployeeDto Employee, IReadOnlyList<string> Warnings);
