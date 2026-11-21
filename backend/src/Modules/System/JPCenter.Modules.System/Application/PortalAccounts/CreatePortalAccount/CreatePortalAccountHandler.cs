using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.PortalAccounts.Common;
using JPCenter.Modules.System.Domain.PortalAccounts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.PortalAccounts.CreatePortalAccount;

/// <summary>
/// AUTH-10: endpoint cần <c>students.profiles.update</c>; cấp cho phụ huynh cần thêm <c>students.contacts.update</c>.
/// Chủ tài khoản phải tồn tại và trong phạm vi chi nhánh (<see cref="PortalOwnerAccess"/>); phụ huynh phải có ≥ 1 con liên kết (HV-13.E3).
/// 1 phụ huynh = 1 tài khoản dù nhiều con (E5) — trùng chủ tài khoản → báo đã có (E1).
/// </summary>
public sealed class CreatePortalAccountHandler(ISystemDbContext db, ICurrentUser user, PortalOwnerAccess owners, PortalCredentialSender credentials,
    IAuditTrail audit)
    : ICommandHandler<CreatePortalAccountCommand, PortalCredentialDto>
{
    public async Task<Result<PortalCredentialDto>> Handle(CreatePortalAccountCommand cmd, CancellationToken ct)
    {
        var ownerType = cmd.OwnerType == "student" ? PortalOwnerType.Student : PortalOwnerType.Guardian;
        if (ownerType == PortalOwnerType.Guardian && !user.Can("students.contacts", PermissionAction.Update))
            return PortalAccountErrors.GuardianPermission;

        if (await owners.FindAsync(ownerType, cmd.OwnerId, PermissionAction.Update, ct) is not { } owner) return PortalAccountErrors.OwnerNotFound;
        if (ownerType == PortalOwnerType.Guardian && owner.ChildCount == 0) return PortalAccountErrors.GuardianWithoutChildren;

        if (await db.PortalAccounts.AnyAsync(x => x.OwnerType == ownerType && x.OwnerId == cmd.OwnerId, ct))
            return PortalAccountErrors.AlreadyExists;

        var username = cmd.Username.Trim().ToLowerInvariant();
        if (await db.PortalAccounts.AnyAsync(x => x.Username == username, ct))
            return PortalAccountErrors.DuplicateUsername(await SuggestAsync(username, ct));

        var account = PortalAccount.Create(ownerType, cmd.OwnerId, username,
            string.IsNullOrWhiteSpace(cmd.Email) ? owner.Email : cmd.Email, owner.Name);
        if (account.IsFailure) return account.Error;
        db.PortalAccounts.Add(account.Value);

        var result = await credentials.IssueAsync(account.Value, ct, () => db.SaveChangesAsync(ct));
        audit.Record(AuditCategory.Data, AuditSeverity.Info,
            ownerType == PortalOwnerType.Student ? "Cấp tài khoản cổng học viên" : "Cấp tài khoản cổng phụ huynh",
            $"{owner.Name} ({username})", result.EmailSent ? "Mật khẩu tạm gửi qua email" : "Mật khẩu tạm đưa trực tiếp");
        return result;
    }

    /// <summary>Thêm hậu tố số nhỏ nhất chưa dùng: hv1000 → hv1000.2</summary>
    private async Task<string> SuggestAsync(string username, CancellationToken ct)
    {
        for (var i = 2; i < 100; i++)
        {
            var candidate = $"{username}.{i}";
            if (!await db.PortalAccounts.AnyAsync(x => x.Username == candidate, ct)) return candidate;
        }
        return $"{username}.{Guid.NewGuid().ToString("N")[..4]}";
    }
}
