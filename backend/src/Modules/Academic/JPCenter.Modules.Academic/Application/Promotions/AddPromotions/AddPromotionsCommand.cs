using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Promotions.AddPromotions;

/// <summary>
/// DT-17 bước 1 — thêm HV đang học của lớp vào danh sách lên lớp (nguồn: GV đề xuất / số liệu / giáo vụ thêm tay).
/// HV đã có trong danh sách → bỏ qua; HV không học lớp này → lỗi. Trả cả danh sách sau khi thêm.
/// </summary>
public sealed record AddPromotionsCommand(Guid ClassId, IReadOnlyList<Guid> StudentIds, PromotionSource Source) : ICommand<IReadOnlyList<PromotionDto>>;

public sealed class AddPromotionsValidator : AbstractValidator<AddPromotionsCommand>
{
    public AddPromotionsValidator()
    {
        RuleFor(x => x.StudentIds).NotEmpty().WithMessage("Chọn ít nhất 1 học viên").Must(s => s.Count <= 200);
        RuleFor(x => x.Source).IsInEnum();
    }
}

public sealed class AddPromotionsHandler(IAcademicDbContext db, ClassReader classes, IEnrollmentLookup enrollments, PromotionReader reader)
    : ICommandHandler<AddPromotionsCommand, IReadOnlyList<PromotionDto>>
{
    public static readonly Error NotInClass = Error.Validation("academic.promotion.not_in_class", "Chỉ thêm được học viên đang học lớp này");

    public async Task<Result<IReadOnlyList<PromotionDto>>> Handle(AddPromotionsCommand cmd, CancellationToken ct)
    {
        var cls = await classes.ForUpdateAsync(cmd.ClassId, AcademicAccess.Classes, PermissionAction.Update, ct);
        if (cls is null) return ClassErrors.NotFound;
        var roster = (await enrollments.RosterAsync(cls.Id.Value, ct)).Select(r => r.StudentId).ToHashSet();
        if (cmd.StudentIds.Any(s => !roster.Contains(s))) return NotInClass;

        var existing = await db.PromotionCandidates.Where(p => p.FromClassId == cls.Id).ToListAsync(ct);
        foreach (var studentId in cmd.StudentIds.Distinct().Where(s => existing.All(p => p.StudentId != s)))
        {
            var created = PromotionCandidate.Create(studentId, cls.BranchId, cls.Id, cmd.Source);
            if (created.IsFailure) return created.Error;
            db.PromotionCandidates.Add(created.Value);
            existing.Add(created.Value);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await reader.ToDtosAsync(existing, ct));
    }
}
