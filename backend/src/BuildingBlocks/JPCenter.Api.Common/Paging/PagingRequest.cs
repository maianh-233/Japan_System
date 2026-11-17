using JPCenter.Application.Common.Paging;

namespace JPCenter.Api.Common.Paging;

/// <summary>Đọc <c>?page=&amp;pageSize=&amp;sort=</c> — dùng với <c>[AsParameters] PagingRequest paging</c>.</summary>
public sealed record PagingRequest(int? Page, int? PageSize, string? Sort)
{
    public PageRequest ToPageRequest() => new(Page ?? 1, PageSize ?? 20, Sort);
}
