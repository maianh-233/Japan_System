using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.System.Application.Abstractions;

namespace JPCenter.Modules.System.Infrastructure.Services;

/// <summary>Đọc <c>permission-catalog.json</c> (nhúng trong assembly) 1 lần khi khởi động.</summary>
internal sealed class PermissionCatalog : IPermissionCatalog
{
    public PermissionCatalog()
    {
        var file = SeedData.Read<CatalogFile>(typeof(PermissionCatalog).Assembly, "permission-catalog.json");
        Modules = file.Modules;
        All = file.Modules.SelectMany(m => m.Groups).SelectMany(g => g.Actions.Select(a => $"{g.Key}.{a}")).ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<PermissionModule> Modules { get; }

    public IReadOnlySet<string> All { get; }

    public bool IsValid(string permission) => All.Contains(permission);

    private sealed record CatalogFile(List<PermissionModule> Modules);
}
