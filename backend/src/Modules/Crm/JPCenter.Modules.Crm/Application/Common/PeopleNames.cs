using JPCenter.Modules.Hr.Contracts;

namespace JPCenter.Modules.Crm.Application.Common;

/// <summary>Tên nhân viên (Sale, người tiếp, người soạn…) — đọc qua Hr.Contracts, 1 lần cho cả danh sách.</summary>
public sealed class PeopleNames(IEmployeeDirectory employees)
{
    public async Task<IReadOnlyDictionary<Guid, string>> OfAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.Where(i => i is { } g && g != Guid.Empty).Select(i => i!.Value).Distinct().ToList();
        if (wanted.Count == 0) return new Dictionary<Guid, string>();
        var found = await employees.FindManyAsync(wanted, ct);
        return found.ToDictionary(x => x.Key, x => x.Value.FullName);
    }
}
