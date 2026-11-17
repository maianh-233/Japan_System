using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.NewCurriculumVersion;

/// <summary>
/// LT-10 — bản nháp phiên bản kế tiếp (v1.0 → v1.1), sao chép nội dung, dùng chung file; gắn vào các khóa đang dùng bản cũ
/// (không đặt mặc định). Đã có bản nháp kế tiếp → chặn (E1).
/// </summary>
public sealed record NewCurriculumVersionCommand(Guid Id) : ICommand<CurriculumDto>;

public sealed class NewCurriculumVersionHandler(IAcademicDbContext db, CurriculumRules rules, CurriculumReader reader, ICurrentUser user,
    IAuditTrail audit, IClock clock) : ICommandHandler<NewCurriculumVersionCommand, CurriculumDto>
{
    public async Task<Result<CurriculumDto>> Handle(NewCurriculumVersionCommand cmd, CancellationToken ct)
    {
        var source = await db.Curricula.AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == new CurriculumId(cmd.Id), ct);
        if (source is null) return CurriculumErrors.NotFound;
        if (await rules.HasDraftSuccessorAsync(source.Id, ct)) return CurriculumErrors.DraftExists;

        var created = source.NewVersion(user.Access?.EmployeeId ?? Guid.Empty);
        if (created.IsFailure) return created.Error;
        var draft = created.Value;
        if (await rules.EnsureVersionUniqueAsync(draft.Code, draft.Version, null, ct) is { IsFailure: true } dup) return dup.Error;
        db.Curricula.Add(draft);

        var courses = await db.Courses.ToListAsync(ct);
        foreach (var course in courses.Where(c => c.Curricula.Any(l => l.CurriculumId == source.Id)))
            course.LinkCurriculum(draft.Id, false, clock.UtcNow);

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Tạo phiên bản mới lộ trình", draft.Name,
            $"{draft.Code} {source.Version} → {draft.Version}");
        return (await reader.FindAsync(draft.Id, ct))!;
    }
}
