using JPCenter.Application.Common.Messaging;

namespace JPCenter.Modules.System.Application.Auth.GetInvitation;

/// <summary>AUTH-05 bước 2 — mở link mời: xem tên, email, vai trò được gán (chưa dùng link).</summary>
public sealed record GetInvitationQuery(string Token) : IQuery<InvitationDto>;

public sealed record InvitationDto(string FullName, string Email, IReadOnlyList<string> Roles, DateTimeOffset ExpiresAt);
