using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.PortalAccounts.Common;

namespace JPCenter.Modules.System.Application.PortalAccounts.SetPortalAccountLock;

/// <summary>AUTH-10.E1 — khóa / mở khóa tài khoản cổng. Khóa → đăng xuất mọi thiết bị ngay.</summary>
public sealed record SetPortalAccountLockCommand(Guid AccountId, bool Locked) : ICommand<PortalAccountDto>;
