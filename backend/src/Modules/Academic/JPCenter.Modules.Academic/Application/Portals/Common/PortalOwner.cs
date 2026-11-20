using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Portals.Common;

/// <summary>
/// Chủ tài khoản cổng (PQ-11): HV = chính mình (<c>OwnerId</c> = Students.Student) · PH = các con (<c>OwnerId</c> = Crm.Lead, tra qua Crm.Contracts)
/// · GV = nhân viên (<c>EmployeeId</c>). Dữ liệu cổng luôn lọc theo đây — không dùng mã quyền.
/// </summary>
public sealed class PortalOwner(ICurrentUser user, IGuardianDirectory guardians)
{
    public Guid AccountId => user.AccountId ?? Guid.Empty;
    public string Name => user.Access?.DisplayName ?? "—";
    public bool IsParent => user.Access?.Portal == Portal.Parent;
    public RequestedBy By => IsParent ? RequestedBy.Parent : RequestedBy.Student;

    /// <summary>GV đang gọi (Hr.Employee) — null nếu tài khoản không gắn hồ sơ nhân sự</summary>
    public Guid? TeacherId => user.Access?.EmployeeId;

    /// <summary>HV tài khoản này được xem / gửi đơn: HV → [mình]; PH → các con</summary>
    public async Task<IReadOnlyList<Guid>> StudentsAsync(CancellationToken ct) => user.Access switch
    {
        { Portal: Portal.Student, OwnerId: { } me } => [me],
        { Portal: Portal.Parent, OwnerId: { } lead } => await guardians.ChildrenOfAsync(lead, ct),
        _ => [],
    };

    /// <summary>
    /// HV đang thao tác: HV bỏ trống / truyền chính mình; PH phải chọn 1 con (chỉ 1 con thì tự chọn). Không thuộc tài khoản → 403 (DT-18.E5).
    /// </summary>
    public async Task<Result<Guid>> StudentAsync(Guid? studentId, CancellationToken ct)
    {
        var mine = await StudentsAsync(ct);
        if (studentId is { } wanted) return mine.Contains(wanted) ? wanted : RequestErrors.NotYourChild;
        return mine.Count == 1 ? mine[0] : RequestErrors.NotYourChild;
    }
}
