using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Users.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Users.GetUsers;

/// <summary>PQ-05 bước 1 — danh sách người dùng (vai trò @ chi nhánh, quyền lẻ, trạng thái, đăng nhập gần nhất).</summary>
/// <param name="Status">"invited" · "active" · "locked" — bỏ trống = tất cả</param>
public sealed record GetUsersQuery(Guid? RoleId, Guid? BranchId, string? Status) : IQuery<IReadOnlyList<UserAccessDto>>;

public sealed class GetUsersHandler(UserReader reader) : IQueryHandler<GetUsersQuery, IReadOnlyList<UserAccessDto>>
{
    public async Task<Result<IReadOnlyList<UserAccessDto>>> Handle(GetUsersQuery query, CancellationToken ct)
    {
        IEnumerable<UserAccessDto> users = await reader.ListAsync(ct);
        if (query.RoleId is { } role) users = users.Where(u => u.Roles.Any(r => r.RoleId == role));
        // Lọc chi nhánh: có vai trò ở chi nhánh đó (hoặc mọi chi nhánh)
        if (query.BranchId is { } branch) users = users.Where(u => u.Roles.Any(r => r.BranchIds.Count == 0 || r.BranchIds.Contains(branch)));
        if (!string.IsNullOrEmpty(query.Status)) users = users.Where(u => u.Status == query.Status);
        return Result.Success<IReadOnlyList<UserAccessDto>>(users.ToList());
    }
}
