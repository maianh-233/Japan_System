using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.PortalAccounts.Common;

namespace JPCenter.Modules.System.Application.PortalAccounts.CreatePortalAccount;

/// <summary>
/// AUTH-10 — cấp tài khoản cổng cho học viên (<c>student</c>) hoặc phụ huynh (<c>guardian</c>).
/// Tên chủ tài khoản lấy từ hồ sơ HV / PH (Contracts) — <see cref="DisplayName"/> client gửi được bỏ qua (giữ để tương thích).
/// Email bỏ trống → dùng email của phụ huynh (nếu có).
/// </summary>
public sealed record CreatePortalAccountCommand(string OwnerType, Guid OwnerId, string? DisplayName, string Username, string? Email)
    : ICommand<PortalCredentialDto>;
