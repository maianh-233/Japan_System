using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Onboardings.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Onboardings.GetOnboardings;

/// <summary>Bộ lọc phiếu onboarding</summary>
public enum OnboardingFilter
{
    /// <summary>Đang onboarding (chưa xong)</summary>
    Active,
    /// <summary>Đã xong (đã tạo hồ sơ + tick đủ)</summary>
    Done,
    All,
}

/// <summary>OB-01 — danh sách phiếu trong phạm vi <c>hr.onboarding.view</c>, ngày bắt đầu gần nhất trước.</summary>
public sealed record GetOnboardingsQuery(OnboardingFilter Filter) : IQuery<IReadOnlyList<OnboardingDto>>;

public sealed class GetOnboardingsHandler(IHrDbContext db, OnboardingDesk desk) : IQueryHandler<GetOnboardingsQuery, IReadOnlyList<OnboardingDto>>
{
    public async Task<Result<IReadOnlyList<OnboardingDto>>> Handle(GetOnboardingsQuery q, CancellationToken ct)
    {
        var list = (await db.Onboardings.AsNoTracking().ToListAsync(ct))
            .Where(o => desk.InScope(o, PermissionAction.View))
            .Where(o => q.Filter switch { OnboardingFilter.Active => !o.IsComplete, OnboardingFilter.Done => o.IsComplete, _ => true })
            .OrderByDescending(o => o.StartDate)
            .ToList();
        return Result.Success(await desk.ToDtosAsync(list, ct));
    }
}
