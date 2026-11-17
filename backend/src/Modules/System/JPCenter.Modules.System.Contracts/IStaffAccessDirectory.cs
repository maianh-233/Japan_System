using JPCenter.Modules.System.Contracts.Dtos;

namespace JPCenter.Modules.System.Contracts;

/// <summary>
/// Ai đang có 1 quyền (tài khoản nhân viên đang hoạt động) — để gửi email / thông báo đúng người phụ trách
/// (vd đổi tài khoản ngân hàng chi nhánh → người có <c>finance.invoices.view</c>; hợp đồng sắp hết hạn → người có <c>hr.employees.update</c>).
/// </summary>
public interface IStaffAccessDirectory
{
    /// <param name="permission">Mã quyền đầy đủ, vd "finance.invoices.view"</param>
    /// <param name="branchId">Chỉ người có quyền ĐÓ tại chi nhánh này (phạm vi toàn hệ thống / gán chi nhánh đó); null = mọi người có quyền</param>
    Task<IReadOnlyList<StaffContact>> WithPermissionAsync(string permission, Guid? branchId, CancellationToken ct);
}
