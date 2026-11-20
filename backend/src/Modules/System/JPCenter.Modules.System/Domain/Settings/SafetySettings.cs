using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>An toàn dữ liệu</summary>
/// <param name="RefundApprovalMin">Hoàn tiền từ số tiền này trở lên cần người thứ 2 duyệt (0 = mọi khoản)</param>
/// <param name="TrashDays">Số ngày giữ trong Thùng rác trước khi xóa hẳn — 1 → 365</param>
public sealed record SafetySettings(Money RefundApprovalMin, int TrashDays);
