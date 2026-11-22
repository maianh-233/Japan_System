using JPCenter.Modules.Hr.Contracts;

namespace JPCenter.Modules.Finance.Application.Common;

/// <summary>Tên người thu / người hoàn / người duyệt (Hr.Employee) — 1 lần cho cả danh sách; không thấy → không có trong kết quả</summary>
public sealed class PeopleNames(IEmployeeDirectory employees)
{
    public async Task<IReadOnlyDictionary<Guid, string>> OfAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.OfType<Guid>().Where(i => i != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0) return new Dictionary<Guid, string>();
        return (await employees.FindManyAsync(wanted, ct)).ToDictionary(x => x.Key, x => x.Value.FullName);
    }
}
