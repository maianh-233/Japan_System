using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.DeleteCurriculum;

/// <summary>
/// LT-11 — xóa bản NHÁP chưa dùng (vào Thùng rác — xóa mềm, nội dung + file giữ nguyên để khôi phục). Bản đã phát hành dùng lưu trữ (E2);
/// đang mặc định / có lớp → chặn (E3). Liên kết với khóa (không mặc định) được gỡ.
/// </summary>
public sealed record DeleteCurriculumCommand(Guid Id) : ICommand;

public sealed class DeleteCurriculumHandler(IAcademicDbContext db, CurriculumRules rules, ClassUsage classes, IAuditTrail audit)
    : ICommandHandler<DeleteCurriculumCommand>
{
    public async Task<Result> Handle(DeleteCurriculumCommand cmd, CancellationToken ct)
    {
        var c = await db.Curricula.AsSplitQuery().FirstOrDefaultAsync(x => x.Id == new CurriculumId(cmd.Id), ct);
        if (c is null) return CurriculumErrors.NotFound;
        if (!c.IsDraft) return CurriculumErrors.NotDraft;
        if (await rules.DefaultCourseCountAsync(c.Id, ct) > 0) return CurriculumErrors.InUse;
        if ((await classes.OpenClassesUsingAsync(cmd.Id, null, ct)).Count > 0) return CurriculumErrors.InUse;

        var courses = await db.Courses.ToListAsync(ct);
        foreach (var course in courses.Where(x => x.Curricula.Any(l => l.CurriculumId == c.Id)))
            course.UnlinkCurriculum(c.Id);
        db.Curricula.Remove(c);

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Xóa lộ trình nháp", c.Name, $"{c.Code} {c.Version}");
        return Result.Success();
    }
}
