using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Roles.GetPermissionCatalog;

/// <summary>Danh mục quyền (module → trang → hành động) để dựng ma trận.</summary>
public sealed record GetPermissionCatalogQuery : IQuery<IReadOnlyList<PermissionModule>>;

public sealed class GetPermissionCatalogHandler(IPermissionCatalog catalog) : IQueryHandler<GetPermissionCatalogQuery, IReadOnlyList<PermissionModule>>
{
    public Task<Result<IReadOnlyList<PermissionModule>>> Handle(GetPermissionCatalogQuery query, CancellationToken ct) =>
        Task.FromResult(Result.Success(catalog.Modules));
}
