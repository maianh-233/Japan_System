namespace JPCenter.Application.Common.Paging;

/// <summary>Tham số phân trang chung: <c>page</c> (từ 1), <c>pageSize</c> (≤ 100), <c>sort</c> ("name" tăng, "-name" giảm).</summary>
public sealed record PageRequest(int Page = 1, int PageSize = 20, string? Sort = null)
{
    public const int MaxPageSize = 100;

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize switch { < 1 => 20, > MaxPageSize => MaxPageSize, _ => PageSize };
}
