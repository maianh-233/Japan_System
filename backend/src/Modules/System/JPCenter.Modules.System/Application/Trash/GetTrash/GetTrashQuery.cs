using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Trash.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Trash.GetTrash;

/// <summary>TR-01 — mục trong Thùng rác theo phạm vi quyền, mới xóa trước. Lead / HV bị ẩn không ở đây (frontend tự ghép).</summary>
/// <param name="BranchId">Lọc chi nhánh — kèm mục không gắn chi nhánh (vd vai trò)</param>
/// <param name="Kind">"module.entity"</param>
public sealed record GetTrashQuery(Guid? BranchId, string? Kind) : IQuery<IReadOnlyList<TrashItemDto>>;

public sealed class GetTrashHandler(TrashBin bin) : IQueryHandler<GetTrashQuery, IReadOnlyList<TrashItemDto>>
{
    public async Task<Result<IReadOnlyList<TrashItemDto>>> Handle(GetTrashQuery query, CancellationToken ct)
    {
        var items = bin.Visible().AsNoTracking();
        if (query.BranchId is { } branch)
        {
            var b = new BranchId(branch);
            items = items.Where(t => t.BranchId == b || t.BranchId == null);
        }
        if (!string.IsNullOrWhiteSpace(query.Kind)) items = items.Where(t => t.Kind == query.Kind);
        var list = await items.OrderByDescending(t => t.DeletedAt).ToListAsync(ct);
        return Result.Success(await bin.ToDtosAsync(list, ct));
    }
}
