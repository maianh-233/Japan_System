using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Abstractions;
using JPCenter.Modules.System.Application.Branches.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.System.Application.Branches.GetBranchOptions;

/// <summary>CN-08 — danh sách chi nhánh cho ô chọn (mọi người đã đăng nhập; giao diện tự lọc chi nhánh được phép / đang hoạt động).</summary>
public sealed record GetBranchOptionsQuery : IQuery<IReadOnlyList<BranchOptionDto>>;

public sealed class GetBranchOptionsHandler(ISystemDbContext db) : IQueryHandler<GetBranchOptionsQuery, IReadOnlyList<BranchOptionDto>>
{
    public async Task<Result<IReadOnlyList<BranchOptionDto>>> Handle(GetBranchOptionsQuery query, CancellationToken ct)
    {
        var list = await db.Branches.AsNoTracking().OrderBy(b => b.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<BranchOptionDto>>(
            list.Select(b => new BranchOptionDto(b.Id.Value, b.Code, b.Name, BranchReader.StatusText(b.Status))).ToList());
    }
}
