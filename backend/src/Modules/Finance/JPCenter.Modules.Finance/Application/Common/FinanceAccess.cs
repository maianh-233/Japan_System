using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Common;

/// <summary>
/// Mã trang quyền Tài chính (trùng <c>frontend/src/config/navigation.ts</c>) + phạm vi chi nhánh của người đang gọi.
/// Người thao tác = hồ sơ nhân sự (Hr.Employee) — tài khoản không gắn nhân sự thì dùng Id tài khoản.
/// </summary>
public sealed class FinanceAccess(ICurrentUser user)
{
    public const string Tuition = "finance.tuition";
    public const string Invoices = "finance.invoices";
    public const string Reconciliation = "finance.reconciliation";
    public const string Debts = "finance.debts";
    public const string TeacherPayroll = "finance.teacher-payroll";
    public const string StaffPayroll = "finance.staff-payroll";
    public const string Commissions = "finance.commissions";

    public static readonly Error BranchNotAllowed = Error.Forbidden("finance.branch_not_allowed",
        "Bạn chỉ thao tác được dữ liệu tài chính của chi nhánh mình được phân công");

    /// <summary>Người đang gọi; null = hệ thống (webhook ngân hàng, job)</summary>
    public Guid? ActorId => user.Access is { } a ? a.EmployeeId ?? a.AccountId : null;

    /// <summary>Chi nhánh được thao tác; null = mọi chi nhánh; rỗng = không có quyền</summary>
    public HashSet<Guid>? Branches(string page, PermissionAction action) => user.AllowedBranches(page, action);

    public bool InBranch(string page, PermissionAction action, Guid branchId) =>
        Branches(page, action) is not { } allowed || allowed.Contains(branchId);
}
