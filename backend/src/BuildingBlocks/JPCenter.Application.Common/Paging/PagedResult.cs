namespace JPCenter.Application.Common.Paging;

/// <summary>Kết quả danh sách có phân trang — khớp dạng <c>{ items, total, page, pageSize }</c> của frontend.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
