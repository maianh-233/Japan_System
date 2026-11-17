using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Hr.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.Common;

/// <summary>Đọc lộ trình ra DTO: nội dung theo thứ tự, tên người soạn (Hr), khóa đang gắn, số lớp dùng</summary>
public sealed class CurriculumReader(IAcademicDbContext db, IEmployeeDirectory employees, ClassUsage classes)
{
    public const string Page = "academic.curriculum";

    public async Task<IReadOnlyList<CurriculumDto>> ListAsync(CancellationToken ct)
    {
        var rows = await Query(db.Curricula).AsSplitQuery().ToListAsync(ct);
        return await MapAsync(rows, ct);
    }

    public async Task<CurriculumDto?> FindAsync(CurriculumId id, CancellationToken ct)
    {
        var row = await Query(db.Curricula.Where(c => c.Id == id)).AsSplitQuery().FirstOrDefaultAsync(ct);
        return row is null ? null : (await MapAsync([row], ct))[0];
    }

    private static IQueryable<Row> Query(IQueryable<Curriculum> source) => source.AsNoTracking().Select(c => new Row(
        c, EF.Property<uint>(c, "xmin"), EF.Property<DateTimeOffset>(c, "CreatedAt"), EF.Property<DateTimeOffset?>(c, "UpdatedAt")));

    private async Task<IReadOnlyList<CurriculumDto>> MapAsync(IReadOnlyList<Row> rows, CancellationToken ct)
    {
        var authors = await employees.FindManyAsync(rows.Select(r => r.Curriculum.AuthorId).Distinct().ToList(), ct);
        var courses = await db.Courses.AsNoTracking().ToListAsync(ct);
        var classCount = await classes.ClassCountByCurriculumAsync(ct);

        return rows.Select(r =>
        {
            var c = r.Curriculum;
            var linked = courses
                .SelectMany(course => course.Curricula.Where(l => l.CurriculumId == c.Id)
                    .Select(l => new CurriculumCourseDto(course.Id.Value, course.Code, course.Name, l.IsDefault)))
                .OrderByDescending(x => x.IsDefault).ThenBy(x => x.CourseCode).ToList();
            return new CurriculumDto(
                c.Id.Value, c.Code, c.Name, LevelText.Of(c.Level), c.Tier, c.EntryRequirement, c.Version, c.PreviousVersionId?.Value,
                c.Textbook, c.Description, c.Tags,
                c.Status,
                c.Lessons.OrderBy(l => l.Order).Select(l => new LessonDto(l.Id.Value, l.Title, l.HasQuiz,
                    l.Chapters.OrderBy(ch => ch.Order).Select(ch => new ChapterDto(ch.Id.Value, ch.Title,
                        ch.Materials.Select(ToDto).ToList())).ToList())).ToList(),
                c.Exams.Select(e => new ExamDto(e.Id.Value, e.Kind, e.Title, e.AfterLessonId.Value, e.Note)).ToList(),
                c.AuthorId, authors.TryGetValue(c.AuthorId, out var a) ? a.FullName : "",
                linked,
                classCount.TryGetValue(c.Id.Value, out var n) ? n : 0,
                r.UpdatedAt ?? r.CreatedAt, r.Version);
        }).ToList();
    }

    public static MaterialDto ToDto(Material m) =>
        new(m.Id.Value, m.Name, m.Type, m.IsFile ? null : m.Url, m.IsFile, m.FileName, m.ContentType, m.Size, m.SharedWithStudents);

    private sealed record Row(Curriculum Curriculum, uint Version, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
}
