using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Rooms.Common;

/// <summary>Quy tắc phòng cần dữ liệu ngoài aggregate: tên trùng trong chi nhánh, chi nhánh còn hoạt động</summary>
public sealed class RoomRules(IAcademicDbContext db, IBranchDirectory branches)
{
    public static readonly Error BranchInactive = Error.Validation("system.branch.inactive", "Chỉ chọn chi nhánh đang hoạt động / đang chuẩn bị");

    /// <summary>Tên duy nhất trong chi nhánh (không phân biệt hoa thường, bỏ khoảng trắng 2 đầu)</summary>
    public async Task<Result> EnsureNameUniqueAsync(Guid branchId, string name, RoomId? except, CancellationToken ct)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var query = db.Rooms.Where(r => r.BranchId == new BranchId(branchId) && r.Name.ToLower() == normalized);
        if (except is { } id) query = query.Where(r => r.Id != id);
        return await query.AnyAsync(ct) ? RoomErrors.DuplicateName(name.Trim()) : Result.Success();
    }

    public async Task<Result> EnsureBranchAsync(Guid branchId, CancellationToken ct) =>
        await branches.FindAsync(branchId, ct) is { AcceptsNewData: true } ? Result.Success() : BranchInactive;
}
