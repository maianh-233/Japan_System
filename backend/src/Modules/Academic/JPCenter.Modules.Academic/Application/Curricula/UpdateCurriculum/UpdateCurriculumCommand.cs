using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.UpdateCurriculum;

/// <summary>LT-04 — sửa thông tin. Bản đã phát hành / lưu trữ chỉ sửa mô tả + thẻ (E1). Sửa đè → 409 (E2, xmin)</summary>
public sealed record UpdateCurriculumCommand(Guid Id, CurriculumInput Input, uint RowVersion) : ICommand<CurriculumDto>;

public sealed class UpdateCurriculumValidator : AbstractValidator<UpdateCurriculumCommand>
{
    public UpdateCurriculumValidator() => RuleFor(x => x.Input).SetValidator(new CurriculumInputValidator());
}

public sealed class UpdateCurriculumHandler(IAcademicDbContext db, CurriculumRules rules, CurriculumReader reader, IAuditTrail audit)
    : ICommandHandler<UpdateCurriculumCommand, CurriculumDto>
{
    public async Task<Result<CurriculumDto>> Handle(UpdateCurriculumCommand cmd, CancellationToken ct)
    {
        var c = await db.Curricula.FirstOrDefaultAsync(x => x.Id == new CurriculumId(cmd.Id), ct);
        if (c is null) return CurriculumErrors.NotFound;
        ((DbContext)db).Entry(c).Property("xmin").OriginalValue = cmd.RowVersion;

        var i = cmd.Input;
        if (await rules.EnsureVersionUniqueAsync(i.Code, i.Version, c.Id, ct) is { IsFailure: true } dup) return dup.Error;
        if (c.UpdateInfo(i.Code, i.Name, i.Level, i.Tier, i.EntryRequirement, i.Version, i.Textbook, i.Description, i.Tags ?? [])
            is { IsFailure: true } r) return r.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Sửa thông tin lộ trình", c.Name, $"{c.Code} {c.Version}");
        return (await reader.FindAsync(c.Id, ct))!;
    }
}
