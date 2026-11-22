using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Domain.Pricing;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Pricing.CheckDiscount;

/// <summary>
/// TC-03 bước 2 — kiểm mã ưu đãi khi ghi danh (CRM-07 / HV-06): đang bật, trong thời gian, đúng chi nhánh / khóa, combo đủ khóa đi kèm
/// (<paramref name="OtherCourseIds"/> — khóa HV đang có / ghi danh cùng), còn lượt (E4, E5). Trả số tiền giảm cho học phí <paramref name="Fee"/>.
/// Chỉ kiểm — lượt dùng trừ khi lập hóa đơn.
/// </summary>
public sealed record CheckDiscountQuery(string? Code, Guid? CourseId, Guid? BranchId, decimal? Fee, IReadOnlyList<Guid> OtherCourseIds)
    : IQuery<DiscountCheckResult>;

/// <param name="Amount">Số tiền giảm (đồng) — % làm tròn 1.000đ, không vượt học phí</param>
public sealed record DiscountCheckResult(Guid DiscountId, string Code, string Name, DiscountKind Kind, decimal Value, decimal Amount);

public sealed class CheckDiscountHandler(IFinanceDbContext db, IClock clock) : IQueryHandler<CheckDiscountQuery, DiscountCheckResult>
{
    public static readonly Error MissingInput = Error.Validation("finance.discount.check_input", "Cần mã ưu đãi, khóa học, chi nhánh và học phí");

    public async Task<Result<DiscountCheckResult>> Handle(CheckDiscountQuery q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.Code) || q.CourseId is not { } courseId || q.BranchId is not { } branchId || q.Fee is not { } fee)
            return MissingInput;
        var code = q.Code.Trim().ToUpperInvariant();
        var d = await db.Discounts.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
        if (d is null) return PricingErrors.DiscountNotFound;
        var ok = d.CheckApplicable(courseId, new BranchId(branchId), clock.Today, q.OtherCourseIds);
        if (ok.IsFailure) return ok.Error;
        var price = Money.FromTrusted(Math.Max(0, decimal.Truncate(fee)));
        return new DiscountCheckResult(d.Id.Value, d.Code, d.Name, d.Kind, d.Value, d.Apply(price).Amount);
    }
}
