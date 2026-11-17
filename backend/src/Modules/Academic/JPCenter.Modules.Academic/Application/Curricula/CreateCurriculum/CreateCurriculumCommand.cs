using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.CreateCurriculum;

/// <summary>
/// LT-02 tạo lộ trình (bản nháp, người soạn = người tạo). LT-03 nhân bản (<paramref name="CloneFromId"/>): sao chép nội dung,
/// dùng chung file; bản nâng cao tự gắn vào các khóa của bản gốc (không đặt mặc định).
/// </summary>
public sealed record CreateCurriculumCommand(CurriculumInput Input, Guid? CloneFromId) : ICommand<CurriculumDto>;

public sealed class CreateCurriculumValidator : AbstractValidator<CreateCurriculumCommand>
{
    public CreateCurriculumValidator() => RuleFor(x => x.Input).SetValidator(new CurriculumInputValidator());
}

public sealed class CreateCurriculumHandler(IAcademicDbContext db, CurriculumRules rules, CurriculumReader reader, ICurrentUser user,
    IAuditTrail audit, IClock clock) : ICommandHandler<CreateCurriculumCommand, CurriculumDto>
{
    public async Task<Result<CurriculumDto>> Handle(CreateCurriculumCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var author = user.Access?.EmployeeId ?? Guid.Empty;
        if (await rules.EnsureVersionUniqueAsync(i.Code, i.Version, null, ct) is { IsFailure: true } dup) return dup.Error;

        Curriculum? source = null;
        if (cmd.CloneFromId is { } fromId)
        {
            source = await db.Curricula.AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(c => c.Id == new CurriculumId(fromId), ct);
            if (source is null) return CurriculumErrors.NotFound;
        }

        var created = source is null
            ? Curriculum.Create(i.Code, i.Name, i.Level, i.Tier, i.EntryRequirement, i.Version, i.Textbook, i.Description, i.Tags ?? [], author)
            : source.CloneAs(i.Code, i.Name, i.Level, i.Tier, i.EntryRequirement, i.Version, i.Textbook, i.Description, i.Tags ?? [], author);
        if (created.IsFailure) return created.Error;
        var curriculum = created.Value;
        db.Curricula.Add(curriculum);

        // Bản nâng cao nhân bản từ bản chuẩn → gắn vào các khóa đang dùng bản gốc (giáo vụ chọn khi mở lớp có đầu vào tốt)
        if (source is not null && curriculum.Tier == CurriculumTier.Advanced)
        {
            var courses = await db.Courses.ToListAsync(ct);
            foreach (var course in courses.Where(c => c.Curricula.Any(l => l.CurriculumId == source.Id)))
                course.LinkCurriculum(curriculum.Id, false, clock.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, source is null ? "Tạo lộ trình" : "Nhân bản lộ trình", curriculum.Name,
            source is null ? $"{curriculum.Code} {curriculum.Version}" : $"{curriculum.Code} {curriculum.Version} ← {source.Code} {source.Version}");
        return (await reader.FindAsync(curriculum.Id, ct))!;
    }
}
