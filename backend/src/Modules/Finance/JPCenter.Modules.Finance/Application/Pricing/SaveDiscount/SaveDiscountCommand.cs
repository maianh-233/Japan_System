using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Pricing.Common;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Pricing.SaveDiscount;

/// <summary>
/// TC-03 bước 1 — tạo (<paramref name="Id"/> null) / sửa ưu đãi: mã 3–15 chữ số (duy nhất), giảm % ≤ 50 (E1), combo ≥ 2 khóa (E2), ngày hợp lệ (E3),
/// giới hạn lượt ≥ số đã dùng. Người có phạm vi chi nhánh chỉ tạo ưu đãi cho chi nhánh mình (không để trống = mọi chi nhánh).
/// </summary>
public sealed record SaveDiscountCommand(Guid? Id, string? Code, string? Name, DiscountKind Kind, decimal Value, IReadOnlyList<Guid> CourseIds,
    IReadOnlyList<Guid> BranchIds, DateOnly From, DateOnly To, int? Limit, string? Note, uint? RowVersion) : ICommand<DiscountDto>;

public sealed class SaveDiscountHandler(IFinanceDbContext db, FinanceAccess access) : ICommandHandler<SaveDiscountCommand, DiscountDto>
{
    public static readonly Error DuplicateCode = Error.Conflict("finance.discount.duplicate_code", "Mã ưu đãi đã tồn tại");

    public async Task<Result<DiscountDto>> Handle(SaveDiscountCommand cmd, CancellationToken ct)
    {
        var action = cmd.Id is null ? PermissionAction.Create : PermissionAction.Update;
        if (access.Branches(FinanceAccess.Tuition, action) is { } allowed
            && (cmd.BranchIds.Count == 0 || cmd.BranchIds.Any(b => !allowed.Contains(b))))
            return FinanceAccess.BranchNotAllowed;

        var details = new DiscountDetails(cmd.Code, cmd.Name, cmd.Kind, cmd.Value, cmd.CourseIds, cmd.BranchIds.Select(b => new BranchId(b)).ToList(),
            cmd.From, cmd.To, cmd.Limit, cmd.Note);
        Discount discount;
        if (cmd.Id is { } id)
        {
            var found = await db.Discounts.FirstOrDefaultAsync(d => d.Id == new DiscountId(id), ct);
            if (found is null) return PricingErrors.DiscountNotFound;
            if (cmd.RowVersion is { } v) ((DbContext)db).Entry(found).Property("xmin").OriginalValue = v;
            if (found.Update(details) is { IsFailure: true } f) return f.Error;
            discount = found;
        }
        else
        {
            var created = Discount.Create(details);
            if (created.IsFailure) return created.Error;
            db.Discounts.Add(discount = created.Value);
        }
        // Mã duy nhất trong các ưu đãi chưa xóa (UQ có lọc) — báo lỗi rõ thay vì vỡ ràng buộc
        if (await db.Discounts.AnyAsync(d => d.Code == discount.Code && d.Id != discount.Id, ct)) return DuplicateCode;

        await db.SaveChangesAsync(ct);
        return discount.ToDto(((DbContext)db).Entry(discount).Property<uint>("xmin").CurrentValue);
    }
}
