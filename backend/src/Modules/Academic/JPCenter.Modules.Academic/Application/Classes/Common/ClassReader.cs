using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Classes.Common;

/// <summary>Bộ lọc danh sách lớp (DT-01) — lọc thêm "sắp kết thúc", "chưa đủ sĩ số" ở frontend</summary>
public sealed record ClassFilter(Guid? BranchId, Guid? CourseId, Guid? TeacherId, ClassStatus? Status);

/// <summary>
/// Đọc lớp ra DTO theo phạm vi quyền (<see cref="AcademicAccess"/>): chi nhánh + phạm vi own = lớp mình là GV / trợ giảng (DT-01.E1).
/// Học phí chỉ trả cho người mở / sửa lớp hoặc xem được hóa đơn (DT-01.E2).
/// </summary>
public sealed class ClassReader(IAcademicDbContext db, AcademicAccess access, ClassRoster roster)
{
    public async Task<IReadOnlyList<ClassDto>> ListAsync(ClassFilter f, CancellationToken ct)
    {
        var source = db.Classes.AsQueryable();
        if (f.CourseId is { } course) source = source.Where(c => c.CourseId == new Domain.Courses.CourseId(course));
        if (f.Status is { } status) source = source.Where(c => c.Status == status);
        var rows = (await Query(source).ToListAsync(ct))
            .Where(r => Visible(r.Class, AcademicAccess.Classes, PermissionAction.View))
            .Where(r => f.BranchId is not { } b || r.Class.BranchId.Value == b)
            .Where(r => f.TeacherId is not { } t || r.Class.TeacherIds.Contains(t) || r.Class.AssistantIds.Contains(t))
            .ToList();
        return await ToDtosAsync(rows, ct);
    }

    /// <summary>Lớp trong phạm vi <paramref name="action"/> của trang lớp — null = không có / ngoài phạm vi (404)</summary>
    public async Task<ClassDto?> FindAsync(Guid id, CancellationToken ct)
    {
        var row = await Query(db.Classes.Where(c => c.Id == new ClassId(id))).FirstOrDefaultAsync(ct);
        if (row is null || !Visible(row.Class, AcademicAccess.Classes, PermissionAction.View)) return null;
        return (await ToDtosAsync([row], ct))[0];
    }

    /// <summary>Đọc lại sau khi ghi (người ghi đã qua kiểm phạm vi)</summary>
    public async Task<ClassDto> ReloadAsync(ClassId id, CancellationToken ct) =>
        (await ToDtosAsync([await Query(db.Classes.Where(c => c.Id == id)).FirstAsync(ct)], ct))[0];

    /// <summary>Lớp được theo dõi để sửa, trong phạm vi <paramref name="action"/> của <paramref name="page"/></summary>
    public async Task<Class?> ForUpdateAsync(Guid id, string page, PermissionAction action, CancellationToken ct)
    {
        var c = await db.Classes.FirstOrDefaultAsync(x => x.Id == new ClassId(id), ct);
        return c is not null && Visible(c, page, action) ? c : null;
    }

    /// <summary>Phạm vi chi nhánh + own (GV / trợ giảng của lớp)</summary>
    public bool Visible(Class c, string page, PermissionAction action)
    {
        if (!access.InBranch(page, action, c.BranchId.Value)) return false;
        return access.OwnOnly(page, action) is not { } me || c.TeacherIds.Contains(me) || c.AssistantIds.Contains(me);
    }

    private async Task<IReadOnlyList<ClassDto>> ToDtosAsync(IReadOnlyList<Row> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(r => r.Class.Id).ToList();
        var courseIds = rows.Select(r => r.Class.CourseId).Distinct().ToList();
        var courses = await db.Courses.AsNoTracking().IgnoreQueryFilters().Where(c => courseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var rooms = await db.Rooms.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var progress = (await db.ClassSessions.AsNoTracking()
                .Where(s => ids.Contains(s.ClassId) && s.Origin != SessionOrigin.Extra)
                .GroupBy(s => s.ClassId)
                .Select(g => new
                {
                    ClassId = g.Key,
                    Confirmed = g.Count(s => s.ConfirmedAt != null),
                    Scheduled = g.Count(s => s.Status == SessionStatus.Scheduled && s.ConfirmedAt == null),
                    Cancelled = g.Count(s => s.Status == SessionStatus.Cancelled),
                })
                .ToListAsync(ct))
            .ToDictionary(p => p.ClassId, p => new ClassProgressDto(p.Confirmed, p.Scheduled, p.Cancelled));
        var enrolled = await roster.EnrolledAsync(ids.Select(i => i.Value).ToList(), ct);
        var seeFee = access.Can(AcademicAccess.Classes, PermissionAction.Create) || access.Can(AcademicAccess.Classes, PermissionAction.Update)
            || access.Can("finance.invoices", PermissionAction.View);

        return rows.Select(r =>
        {
            var c = r.Class;
            return new ClassDto(c.Id.Value, c.CourseId.Value, c.CurriculumId?.Value, c.Code, c.Name, courses.GetValueOrDefault(c.CourseId) ?? "",
                c.BranchId.Value, c.TeacherIds, c.AssistantIds, c.RoomId?.Value, c.RoomId is { } room ? rooms.GetValueOrDefault(room) : null,
                c.Online, c.MeetingUrl, c.PlannedStartDate, c.PlannedEndDate, c.ActualStartDate, c.ActualEndDate, c.Weekdays,
                TimeText.Of(c.StartTime), TimeText.Of(c.EndTime), c.TotalHours, c.MinStudents, c.MaxStudents, seeFee ? c.Fee.Amount : null,
                c.Status, c.PreviousClassId?.Value, c.PauseReason, c.CancelReason, c.Note, c.SessionCount,
                progress.GetValueOrDefault(c.Id) ?? new ClassProgressDto(0, 0, 0),
                enrolled.TryGetValue(c.Id.Value, out var n) ? n : null, r.CreatedAt, r.Version);
        }).OrderBy(c => c.Status).ThenByDescending(c => c.PlannedStartDate).ThenBy(c => c.Code).ToList();
    }

    private static IQueryable<Row> Query(IQueryable<Class> source) => source.AsNoTracking().Select(c => new Row(
        c, EF.Property<uint>(c, "xmin"), EF.Property<DateTimeOffset>(c, "CreatedAt")));

    private sealed record Row(Class Class, uint Version, DateTimeOffset CreatedAt);
}
