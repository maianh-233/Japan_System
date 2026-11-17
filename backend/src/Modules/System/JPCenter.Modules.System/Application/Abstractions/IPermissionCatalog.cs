namespace JPCenter.Modules.System.Application.Abstractions;

/// <summary>
/// Danh mục mã quyền — giống hệt <c>PERM_CATALOG</c> của frontend (xuất bằng <c>npm run export-seed</c>).
/// Mã quyền ngoài danh mục bị từ chối khi lưu role / quyền lẻ.
/// </summary>
public interface IPermissionCatalog
{
    IReadOnlyList<PermissionModule> Modules { get; }

    /// <summary>Mọi mã quyền đầy đủ "module.page.action".</summary>
    IReadOnlySet<string> All { get; }

    bool IsValid(string permission);
}

public sealed record PermissionModule(string Key, string Label, IReadOnlyList<PermissionGroup> Groups);

/// <param name="Key">"crm.leads"</param>
/// <param name="Actions">"view", "create", "update", "delete", "export", "approve"</param>
public sealed record PermissionGroup(string Key, string Label, IReadOnlyList<string> Actions);
