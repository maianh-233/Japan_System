using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;

namespace JPCenter.Modules.Work.Application.Common;

/// <summary>
/// Nhân sự cho Work — đọc qua <see cref="IEmployeeDirectory"/> (Hr.Contracts), nạp 1 lần / request.
/// Người đang đăng nhập (<see cref="MeId"/>) là hồ sơ Hr.Employee gắn tài khoản; không gắn → không dùng được trang cá nhân.
/// </summary>
public sealed class WorkPeople(IEmployeeDirectory directory, ICurrentUser user)
{
    private IReadOnlyList<EmployeeSummary>? _active;

    public Guid? MeId => user.Access?.EmployeeId;

    public string MeName => user.Access?.DisplayName ?? "Hệ thống";

    /// <summary>Nhân sự chưa nghỉ việc</summary>
    public async Task<IReadOnlyList<EmployeeSummary>> ActiveAsync(CancellationToken ct) => _active ??= await directory.ListActiveAsync(ct);

    public Task<EmployeeSummary?> FindAsync(Guid id, CancellationToken ct) => directory.FindAsync(id, ct);

    public async Task<EmployeeSummary?> MeAsync(CancellationToken ct) => MeId is { } id ? await directory.FindAsync(id, ct) : null;

    /// <summary>Họ tên theo Id (kể cả người đã nghỉ)</summary>
    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new Dictionary<Guid, string>();
        return (await directory.FindManyAsync(list, ct)).ToDictionary(x => x.Key, x => x.Value.FullName);
    }

    /// <summary>Cấp dưới (trực tiếp + gián tiếp) của người đang đăng nhập</summary>
    public async Task<IReadOnlyList<ReportEntry>> MyReportsAsync(CancellationToken ct) =>
        MeId is { } me ? await directory.ReportsOfAsync(me, ct) : [];
}
