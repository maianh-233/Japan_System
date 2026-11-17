using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.ArchiveCurriculum;

/// <summary>LT-11 — lưu trữ (không chọn được khi mở lớp mới; lớp đang học dùng tiếp). Đang là mặc định của khóa → chặn (E1)</summary>
public sealed record ArchiveCurriculumCommand(Guid Id) : ICommand<CurriculumDto>;

public sealed class ArchiveCurriculumHandler(IAcademicDbContext db, CurriculumRules rules, CurriculumReader reader, IAuditTrail audit)
    : ICommandHandler<ArchiveCurriculumCommand, CurriculumDto>
{
    public async Task<Result<CurriculumDto>> Handle(ArchiveCurriculumCommand cmd, CancellationToken ct)
    {
        var c = await db.Curricula.FirstOrDefaultAsync(x => x.Id == new CurriculumId(cmd.Id), ct);
        if (c is null) return CurriculumErrors.NotFound;
        var defaults = await rules.DefaultCourseCountAsync(c.Id, ct);
        if (defaults > 0) return CurriculumErrors.IsDefault(defaults);
        if (c.Archive() is { IsFailure: true } r) return r.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Lưu trữ lộ trình", c.Name, $"{c.Code} {c.Version}");
        return (await reader.FindAsync(c.Id, ct))!;
    }
}
