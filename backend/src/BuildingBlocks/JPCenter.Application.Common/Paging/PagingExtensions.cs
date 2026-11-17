using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Application.Common.Paging;

public static class PagingExtensions
{
    /// <summary>Đếm tổng + lấy 1 trang. Gọi SAU khi đã lọc và sắp xếp.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.SafePage - 1) * page.SafePageSize).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<T>(items, total, page.SafePage, page.SafePageSize);
    }

    /// <summary>
    /// Sắp xếp theo chuỗi <c>sort</c> từ client — CHỈ các cột trong <paramref name="allowed"/> (chống sắp xếp tùy ý / dò cột).
    /// Không khớp → dùng <paramref name="fallback"/>.
    /// </summary>
    public static IOrderedQueryable<T> SortBy<T>(this IQueryable<T> query, string? sort,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> allowed, Expression<Func<T, object?>> fallback)
    {
        var desc = sort?.StartsWith('-') == true;
        var key = sort?.TrimStart('-', '+');
        if (key is null || !allowed.TryGetValue(key, out var selector)) return query.OrderBy(fallback);
        return desc ? query.OrderByDescending(selector) : query.OrderBy(selector);
    }
}
