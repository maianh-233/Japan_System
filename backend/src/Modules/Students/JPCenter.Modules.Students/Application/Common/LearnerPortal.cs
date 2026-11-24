using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Application.Common;

/// <summary>
/// Cổng HV / PH (HV-15.E5, HV-16 cổng): HV của tài khoản — HV → chính mình; PH → con được chọn (<c>studentId</c>), chỉ 1 con thì không cần chọn.
/// HV không thuộc tài khoản → <c>students.student.not_found</c> (không lộ HV khác có tồn tại).
/// </summary>
public sealed class LearnerPortal(ICurrentUser user, IGuardianDirectory guardians)
{
    public async Task<Result<Guid>> StudentAsync(Guid? studentId, CancellationToken ct)
    {
        IReadOnlyList<Guid> mine = user.Access switch
        {
            { Portal: Portal.Student, OwnerId: { } me } => [me],
            { Portal: Portal.Parent, OwnerId: { } lead } => await guardians.ChildrenOfAsync(lead, ct),
            _ => [],
        };
        if (studentId is { } wanted) return mine.Contains(wanted) ? wanted : StudentErrors.NotFound;
        return mine.Count == 1 ? mine[0] : StudentErrors.NotFound;
    }
}
