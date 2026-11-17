using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.PortalAccounts.Common;

namespace JPCenter.Modules.System.Application.PortalAccounts.GetPortalAccounts;

/// <summary>Danh sách tài khoản cổng (tab Tài khoản ở hồ sơ HV / PH) — lọc theo loại và / hoặc chủ tài khoản.</summary>
public sealed record GetPortalAccountsQuery(string? OwnerType, Guid? OwnerId) : IQuery<IReadOnlyList<PortalAccountDto>>;
