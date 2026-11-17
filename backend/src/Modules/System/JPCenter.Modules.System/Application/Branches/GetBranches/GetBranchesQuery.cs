using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Application.Branches.GetBranches;

/// <summary>CN-01 — danh sách chi nhánh + số liệu. Tìm theo tên / mã / địa chỉ / SĐT / email (không dấu), lọc trạng thái.</summary>
/// <param name="Status">"preparing" · "active" · "closed" — trống = tất cả</param>
public sealed record GetBranchesQuery(string? Status, string? Q) : IQuery<IReadOnlyList<BranchDto>>;

public sealed class GetBranchesHandler(BranchReader reader) : IQueryHandler<GetBranchesQuery, IReadOnlyList<BranchDto>>
{
    public async Task<Result<IReadOnlyList<BranchDto>>> Handle(GetBranchesQuery query, CancellationToken ct)
    {
        IEnumerable<BranchDto> list = await reader.ListAsync(ct);
        if (!string.IsNullOrEmpty(query.Status)) list = list.Where(b => b.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = Fold(query.Q);
            list = list.Where(b => Fold($"{b.Name} {b.Code} {b.Address} {b.Phone} {b.Email}").Contains(q, StringComparison.Ordinal));
        }
        return Result.Success<IReadOnlyList<BranchDto>>(list.ToList());
    }

    /// <summary>Bỏ dấu tiếng Việt + chữ thường để tìm "cau giay" ra "Cầu Giấy"</summary>
    private static string Fold(string s)
    {
        var d = s.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(global::System.Text.NormalizationForm.FormD);
        return new string(d.Where(c => global::System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            != global::System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
    }
}
