using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.Modules.Students.Contracts;

namespace JPCenter.Modules.Library.Application.Documents.Common;

/// <summary>Người đang xem tài liệu (ghi vào document_views + watermark)</summary>
/// <param name="Contact">Email / mã in vào watermark — nhân viên: email công việc · HV: mã HV · PH: email hoặc SĐT</param>
public sealed record ViewerIdentity(ViewerType Type, Guid Id, string Name, string? Role, string? Contact);

/// <summary>
/// Quy tắc vàng 2 (user story 16) — xác định người xem từ tài khoản đang gọi: nhân viên / GV (Hr.Contracts lấy email),
/// HV (mã HV qua Students.Contracts), PH (email / SĐT qua Crm.Contracts — lượt xem <c>viewer_type = guardian</c>, TV-07).
/// </summary>
public sealed class LibraryViewer(ICurrentUser user, IEmployeeDirectory employees, IEnrollmentLookup students, IGuardianDirectory guardians)
{
    public async Task<ViewerIdentity?> CurrentAsync(CancellationToken ct)
    {
        if (user.Access is not { } me) return null;
        switch (me.Portal)
        {
            case Portal.Student when me.OwnerId is { } sid:
                var st = (await students.SummariesAsync([sid], ct)).FirstOrDefault();
                return new ViewerIdentity(ViewerType.Student, sid, st?.FullName ?? me.DisplayName, "Học viên", st?.Code);
            case Portal.Parent when me.OwnerId is { } lead:
                var g = (await guardians.ProfilesAsync([lead], ct)).FirstOrDefault();
                return new ViewerIdentity(ViewerType.Guardian, lead, g?.FullName ?? me.DisplayName, "Phụ huynh", g?.Email ?? g?.Phone);
            case Portal.Staff or Portal.Teacher:
                var emp = me.EmployeeId is { } eid ? await employees.FindAsync(eid, ct) : null;
                var role = me.Portal == Portal.Teacher ? "Giáo viên" : me.RoleName;
                return new ViewerIdentity(ViewerType.Staff, me.EmployeeId ?? me.AccountId, emp?.FullName ?? me.DisplayName, role, emp?.Email);
            default:
                return null;
        }
    }
}
