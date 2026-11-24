using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll.Events;

/// <summary>Bảng lương tháng đã chốt (lần đầu hoặc chốt lại sau khi mở khóa) — gửi phiếu lương, chặn sửa công / buổi dạy tháng đó.</summary>
public sealed record PayrollLocked(PayrollLockId LockId, PayrollKind Kind, string Month, BranchId BranchId, Money Total) : IDomainEvent;
