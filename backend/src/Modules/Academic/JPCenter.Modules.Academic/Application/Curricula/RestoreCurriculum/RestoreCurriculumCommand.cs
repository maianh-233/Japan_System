using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.RestoreCurriculum;

/// <summary>LT-11 — khôi phục bản lưu trữ về đã phát hành</summary>
public sealed record RestoreCurriculumCommand(Guid Id) : ICommand<CurriculumDto>;

public sealed class RestoreCurriculumHandler(IAcademicDbContext db, CurriculumReader reader, IAuditTrail audit)
    : ICommandHandler<RestoreCurriculumCommand, CurriculumDto>
{
    public async Task<Result<CurriculumDto>> Handle(RestoreCurriculumCommand cmd, CancellationToken ct)
    {
        var c = await db.Curricula.FirstOrDefaultAsync(x => x.Id == new CurriculumId(cmd.Id), ct);
        if (c is null) return CurriculumErrors.NotFound;
        if (c.Restore() is { IsFailure: true } r) return r.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Khôi phục lộ trình", c.Name, $"{c.Code} {c.Version}");
        return (await reader.FindAsync(c.Id, ct))!;
    }
}
