using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Onboardings.Common;

/// <summary>1 mục checklist — khớp <c>OnboardingItem</c> frontend (<c>doneBy</c> = tên người tick, "Hệ thống" khi tự tick)</summary>
public sealed record OnboardingItemDto(string Key, OnboardingGroup Group, string Label, bool Done, string? DoneBy, DateTimeOffset? DoneAt);

/// <summary>Phiếu onboarding — khớp <c>Onboarding</c> frontend (<c>mentor</c> = tên), thêm <c>mentorId</c>, tiến độ %, đã xong</summary>
public sealed record OnboardingDto(Guid Id, Guid? CandidateId, string FullName, string Phone, string Email, string Position, Department Department,
    EmployeeKind Kind, Guid BranchId, DateOnly StartDate, string Mentor, Guid MentorId, IReadOnlyList<OnboardingItemDto> Items, Guid? EmployeeId,
    DateTimeOffset CreatedAt, int Progress, bool Complete);

/// <summary>Tìm phiếu trong phạm vi <c>hr.onboarding</c> (theo chi nhánh của phiếu) và đọc ra DTO.</summary>
public sealed class OnboardingDesk(IHrDbContext db, HrScope scope)
{
    public bool InScope(Onboarding o, PermissionAction action) => scope.InBranch(HrScope.Onboarding, action, o.BranchId.Value);

    public async Task<Onboarding?> FindForUpdateAsync(Guid id, CancellationToken ct)
    {
        var o = await db.Onboardings.FirstOrDefaultAsync(x => x.Id == new OnboardingId(id), ct);
        return o is not null && InScope(o, PermissionAction.Update) ? o : null;
    }

    public async Task<IReadOnlyList<OnboardingDto>> ToDtosAsync(IReadOnlyList<Onboarding> list, CancellationToken ct)
    {
        var ids = list.Select(o => o.Id).ToList();
        var created = await db.Onboardings.AsNoTracking().Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, At = EF.Property<DateTimeOffset>(o, "CreatedAt") }).ToDictionaryAsync(x => x.Id, x => x.At, ct);
        var mentorIds = list.Select(o => o.MentorId).Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => mentorIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
        return list.Select(o => new OnboardingDto(o.Id.Value, o.CandidateId?.Value, o.FullName, o.Phone, o.Email, o.Position, o.Department, o.Kind,
            o.BranchId.Value, o.StartDate, names.GetValueOrDefault(o.MentorId, "?"), o.MentorId.Value,
            o.Items.Select(i => new OnboardingItemDto(i.Key, i.Group, i.Label, i.Done, i.DoneByName, i.DoneAt)).ToList(),
            o.EmployeeId?.Value, created.GetValueOrDefault(o.Id), o.Progress, o.IsComplete)).ToList();
    }

    public async Task<OnboardingDto> ToDtoAsync(Onboarding o, CancellationToken ct) => (await ToDtosAsync([o], ct))[0];
}
