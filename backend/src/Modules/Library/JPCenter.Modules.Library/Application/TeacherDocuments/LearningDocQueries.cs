using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.TeacherDocuments.Common;
using JPCenter.Modules.Library.Domain.TeacherDocuments;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.TeacherDocuments;

/// <summary>ND-10 / ND-11 — cổng HV / PH › Tài liệu: tài liệu GV gửi cho các lớp HV đã ghi danh, đã xem chưa</summary>
public sealed record GetLearningDocsQuery(Guid? StudentId) : IQuery<IReadOnlyList<TeacherDocDto>>;

/// <summary>ND-10.E2 — HV / PH mở tài liệu → link tạm; HV mở lần đầu → ghi lượt xem (PH mở không tính)</summary>
public sealed record OpenLearningDocCommand(Guid Id, Guid? StudentId) : ICommand<DocLinkDto>;

public sealed class LearningDocHandlers(ILibraryDbContext db, TeacherDocs docs, IClock clock) :
    IQueryHandler<GetLearningDocsQuery, IReadOnlyList<TeacherDocDto>>,
    ICommandHandler<OpenLearningDocCommand, DocLinkDto>
{
    public async Task<Result<IReadOnlyList<TeacherDocDto>>> Handle(GetLearningDocsQuery q, CancellationToken ct)
    {
        var student = await docs.StudentAsync(q.StudentId, ct);
        if (student.IsFailure) return student.Error;
        var classes = await docs.ClassesOfAsync(student.Value, ct);
        if (classes.Count == 0) return Result.Success<IReadOnlyList<TeacherDocDto>>([]);
        var rows = await db.TeacherDocuments.AsNoTracking().Where(d => d.ClassIds.Any(c => classes.Contains(c)))
            .Select(d => new { D = d, Created = EF.Property<DateTimeOffset>(d, "CreatedAt") }).ToListAsync(ct);
        return rows.OrderByDescending(r => r.Created)
            .Select(r => TeacherDocs.ToDto(r.D, r.Created, forOwner: false, student.Value) with { ClassIds = r.D.ClassIds.Where(classes.Contains).ToList() })
            .ToList();
    }

    public async Task<Result<DocLinkDto>> Handle(OpenLearningDocCommand cmd, CancellationToken ct)
    {
        var student = await docs.StudentAsync(cmd.StudentId, ct);
        if (student.IsFailure) return student.Error;
        var classes = await docs.ClassesOfAsync(student.Value, ct);
        var d = await db.TeacherDocuments.FirstOrDefaultAsync(x => x.Id == new TeacherDocumentId(cmd.Id), ct);
        if (d is null || !d.ClassIds.Any(classes.Contains)) return TeacherDocumentErrors.NotFound;
        if (docs.IsStudent && d.MarkViewed(student.Value, clock.UtcNow)) await db.SaveChangesAsync(ct);
        return await docs.LinkAsync(d, ct);
    }
}
