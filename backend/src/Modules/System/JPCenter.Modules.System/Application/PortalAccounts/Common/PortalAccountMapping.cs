using JPCenter.Modules.System.Domain.PortalAccounts;

namespace JPCenter.Modules.System.Application.PortalAccounts.Common;

public static class PortalAccountMapping
{
    public static PortalAccountDto ToDto(this PortalAccount a) => new(
        a.Id.Value,
        a.OwnerType == PortalOwnerType.Student ? "student" : "guardian",
        a.OwnerId, a.DisplayName, a.Username, a.Email,
        a.Status switch { PortalAccountStatus.Active => "active", PortalAccountStatus.Inactive => "inactive", _ => "locked" },
        a.MustChangePassword, a.LastLoginAt);
}
