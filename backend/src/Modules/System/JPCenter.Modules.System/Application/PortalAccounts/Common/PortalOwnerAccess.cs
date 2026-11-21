using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.System.Domain.PortalAccounts;

namespace JPCenter.Modules.System.Application.PortalAccounts.Common;

/// <summary>
/// Chủ tài khoản cổng (HV-13 / AUTH-10): học viên (Students) hoặc phụ huynh = lead có liên kết HV (Crm) — đọc qua Contracts.
/// Phạm vi: HV theo chi nhánh của HV (<c>students.profiles</c>); PH theo chi nhánh của PH HOẶC của con (<c>students.contacts</c>, như HV-12).
/// Ngoài phạm vi / không tồn tại → coi như không có (404).
/// </summary>
public sealed class PortalOwnerAccess(ICurrentUser user, IEnrollmentLookup students, IGuardianDirectory guardians)
{
    public const string StudentPage = "students.profiles";
    public const string GuardianPage = "students.contacts";

    /// <param name="ChildCount">Số HV đang liên kết (phụ huynh); học viên = 0</param>
    public sealed record Owner(Guid Id, string Name, string? Email, int ChildCount);

    /// <summary>1 chủ tài khoản trong phạm vi của hành động; null = không thấy</summary>
    public async Task<Owner?> FindAsync(PortalOwnerType type, Guid ownerId, PermissionAction action, CancellationToken ct) =>
        (await VisibleAsync([(type, ownerId)], action, ct)).GetValueOrDefault((type, ownerId));

    /// <summary>Các chủ tài khoản trong phạm vi (đọc theo lô)</summary>
    public async Task<Dictionary<(PortalOwnerType, Guid), Owner>> VisibleAsync(IReadOnlyCollection<(PortalOwnerType Type, Guid Id)> owners,
        PermissionAction action, CancellationToken ct)
    {
        var result = new Dictionary<(PortalOwnerType, Guid), Owner>();
        var studentIds = owners.Where(o => o.Type == PortalOwnerType.Student).Select(o => o.Id).Distinct().ToList();
        var guardianIds = user.Can(GuardianPage, action)
            ? owners.Where(o => o.Type == PortalOwnerType.Guardian).Select(o => o.Id).Distinct().ToList()
            : [];

        var profiles = guardianIds.Count == 0 ? [] : await guardians.ProfilesAsync(guardianIds, ct);
        var summaries = (await students.SummariesAsync(studentIds.Concat(profiles.SelectMany(p => p.ChildIds)).Distinct().ToList(), ct))
            .ToDictionary(s => s.Id);

        foreach (var id in studentIds)
            if (summaries.GetValueOrDefault(id) is { } s && user.InBranch(StudentPage, action, s.BranchId))
                result[(PortalOwnerType.Student, id)] = new Owner(id, s.FullName, null, 0);

        foreach (var p in profiles)
        {
            var inScope = user.InBranch(GuardianPage, action, p.BranchId)
                || p.ChildIds.Any(c => summaries.GetValueOrDefault(c) is { } s && user.InBranch(GuardianPage, action, s.BranchId));
            if (inScope) result[(PortalOwnerType.Guardian, p.LeadId)] = new Owner(p.LeadId, p.FullName, p.Email, p.ChildIds.Count);
        }
        return result;
    }
}
