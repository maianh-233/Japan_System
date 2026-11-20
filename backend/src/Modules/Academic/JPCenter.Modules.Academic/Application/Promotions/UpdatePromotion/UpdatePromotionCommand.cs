using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Promotions.UpdatePromotion;

/// <summary>
/// DT-17 bước 2 — chọn / đổi lớp đích (lớp chờ khai giảng cùng chi nhánh) và giữ chỗ tạm có hạn (mặc định khai giảng − 7 ngày) hoặc trả chỗ.
/// Giữ chỗ không tính sĩ số; vượt sĩ số tối đa chỉ cảnh báo (E2).
/// </summary>
public sealed record UpdatePromotionCommand(Guid Id, Guid? ToClassId, bool HoldSeat, DateOnly? HoldUntil) : ICommand<PromotionResult>;

public sealed record PromotionResult(PromotionDto Promotion, IReadOnlyList<string> Warnings);

public sealed class UpdatePromotionHandler(IAcademicDbContext db, ClassReader classes, ClassRoster roster, PromotionReader reader, IClock clock)
    : ICommandHandler<UpdatePromotionCommand, PromotionResult>
{
    public static readonly Error InvalidTarget = Error.Validation("academic.promotion.invalid_target",
        "Lớp lên phải là lớp đang chờ khai giảng của cùng chi nhánh");

    public async Task<Result<PromotionResult>> Handle(UpdatePromotionCommand cmd, CancellationToken ct)
    {
        var p = await db.PromotionCandidates.FirstOrDefaultAsync(x => x.Id == new PromotionCandidateId(cmd.Id), ct);
        if (p is null || await classes.ForUpdateAsync(p.FromClassId.Value, AcademicAccess.Classes, PermissionAction.Update, ct) is null)
            return PromotionErrors.NotFound;

        Class? target = null;
        if (cmd.ToClassId is { } tid)
        {
            target = await db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == new ClassId(tid), ct);
            if (target is null || target.Status != ClassStatus.Enrolling || target.BranchId != p.BranchId) return InvalidTarget;
        }
        if (p.ChooseTarget(target?.Id) is { IsFailure: true } chosen) return chosen.Error;

        var warnings = new List<string>();
        if (cmd.HoldSeat)
        {
            if (target is null) return PromotionErrors.ToClassRequired;
            var until = cmd.HoldUntil ?? PromotionCandidate.DefaultHoldUntil(target.ActualStartDate ?? target.PlannedStartDate, clock.Today);
            if (p.HoldSeat(target.Id, until, clock.Today) is { IsFailure: true } held) return held.Error;

            var enrolled = await roster.EnrolledAsync(target.Id.Value, ct) ?? 0;
            var holds = await db.PromotionCandidates.CountAsync(x => x.ToClassId == target.Id && x.SeatHeld && x.Id != p.Id, ct) + 1;
            if (enrolled + holds > target.MaxStudents)
                warnings.Add($"Giữ chỗ vượt sĩ số: {enrolled} ghi danh + {holds} chỗ giữ / tối đa {target.MaxStudents}");
        }
        else p.ReleaseSeat();

        await db.SaveChangesAsync(ct);
        return new PromotionResult((await reader.ToDtosAsync([p], ct))[0], warnings);
    }
}
