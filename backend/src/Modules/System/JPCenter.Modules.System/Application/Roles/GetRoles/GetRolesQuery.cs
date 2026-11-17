using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Roles.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Roles.GetRoles;

/// <summary>PQ-01 — danh sách vai trò + ma trận quyền.</summary>
public sealed record GetRolesQuery : IQuery<IReadOnlyList<RoleDto>>;

public sealed class GetRolesHandler(RoleReader reader) : IQueryHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(GetRolesQuery query, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(ct));
}
