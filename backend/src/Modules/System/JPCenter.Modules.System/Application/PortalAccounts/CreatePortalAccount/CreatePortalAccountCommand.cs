using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.PortalAccounts.Common;

namespace JPCenter.Modules.System.Application.PortalAccounts.CreatePortalAccount;

/// <summary>
/// AUTH-10 — cấp tài khoản cổng cho học viên (<c>student</c>) hoặc phụ huynh (<c>guardian</c>).
/// <see cref="DisplayName"/>: tên chủ tài khoản (frontend gửi kèm vì module Học viên / CRM chưa có API).
/// </summary>
public sealed record CreatePortalAccountCommand(string OwnerType, Guid OwnerId, string DisplayName, string Username, string? Email)
    : ICommand<PortalCredentialDto>;
