namespace JPCenter.Modules.System.Application.Branches.Common;

/// <summary>Chi nhánh — khớp <c>BranchProfile</c> ở <c>frontend/src/features/system/types.ts</c> (+ tên quản lý, số liệu, version).</summary>
/// <param name="Status">"preparing" · "active" · "closed"</param>
/// <param name="OpenedAt">YYYY-MM-DD</param>
/// <param name="Version">Gửi lại khi sửa — người khác vừa sửa thì báo tải lại (CN-03.E3)</param>
public sealed record BranchDto(
    Guid Id,
    string Code,
    string Name,
    string Address,
    string Phone,
    string Email,
    Guid? ManagerId,
    string? ManagerName,
    string OpenHours,
    string Status,
    DateOnly OpenedAt,
    BankDto? Bank,
    string InvoicePrefix,
    string? Note,
    BranchStatsDto Stats,
    uint Version);

/// <param name="Bank">Tên ngân hàng</param>
/// <param name="Account">Số tài khoản</param>
/// <param name="Holder">Chủ tài khoản (viết hoa)</param>
public sealed record BankDto(string Bank, string Account, string Holder);

/// <summary>
/// Số liệu chi nhánh (CN-01). <c>null</c> = module nguồn chưa có (Đào tạo, Học viên, Tài chính) → giao diện hiện "—".
/// </summary>
public sealed record BranchStatsDto(int Staff, int Teachers, int? Rooms, int? ActiveClasses, int? Students, decimal? Collected, decimal? Receivable);

/// <summary>Kết quả đổi trạng thái — kèm cảnh báo không chặn (vd chưa khai báo tài khoản ngân hàng — CN-05.E3).</summary>
public sealed record BranchStatusResult(BranchDto Branch, IReadOnlyList<string> Warnings);

/// <summary>Chi nhánh cho ô chọn (CN-08) — mọi người đã đăng nhập.</summary>
public sealed record BranchOptionDto(Guid Id, string Code, string Name, string Status);

/// <summary>Tài khoản ngân hàng gửi lên khi thêm / sửa — để trống cả 3 = xóa.</summary>
public sealed record BankInput(string? Bank, string? Account, string? Holder);
