using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Rooms.Common;

/// <summary>Đọc phòng + báo hỏng theo phạm vi chi nhánh của quyền <c>academic.rooms.view</c>; lớp đang mở dùng phòng.</summary>
public sealed class RoomReader(IAcademicDbContext db, AcademicAccess access, PeopleNames names)
{
    public async Task<IReadOnlyList<RoomDto>> ListAsync(Guid? branchId, CancellationToken ct)
    {
        var allowed = access.Branches(AcademicAccess.Rooms, PermissionAction.View);
        var source = db.Rooms.AsQueryable();
        if (branchId is { } b) source = source.Where(r => r.BranchId == new BranchId(b));
        var rows = (await Query(source).ToListAsync(ct)).Where(r => allowed is null || allowed.Contains(r.Room.BranchId.Value));
        var open = await OpenClassesAsync(ct);
        return rows.Select(r => ToDto(r, open)).OrderBy(r => r.BranchId).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<RoomDto?> FindAsync(Guid id, CancellationToken ct)
    {
        var r = await Query(db.Rooms.Where(x => x.Id == new RoomId(id))).FirstOrDefaultAsync(ct);
        if (r is null || !access.InBranch(AcademicAccess.Rooms, PermissionAction.View, r.Room.BranchId.Value)) return null;
        return ToDto(r, await OpenClassesAsync(ct));
    }

    public async Task<RoomDto> ReloadAsync(RoomId id, CancellationToken ct) =>
        ToDto(await Query(db.Rooms.Where(x => x.Id == id)).FirstAsync(ct), await OpenClassesAsync(ct));

    /// <summary>Ô chọn: phòng chưa ngừng (mọi nhân viên — form lớp, buổi, lịch hẹn)</summary>
    public async Task<IReadOnlyList<RoomOptionDto>> OptionsAsync(Guid? branchId, CancellationToken ct)
    {
        var query = db.Rooms.AsNoTracking().Where(r => r.Status != RoomStatus.Inactive);
        if (branchId is { } b) query = query.Where(r => r.BranchId == new BranchId(b));
        return (await query.ToListAsync(ct))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new RoomOptionDto(r.Id.Value, r.BranchId.Value, r.Name, r.Capacity, r.Status, r.MaintenanceReason))
            .ToList();
    }

    public async Task<IReadOnlyList<RoomIssueDto>> IssuesAsync(Guid? roomId, CancellationToken ct)
    {
        var allowed = access.Branches(AcademicAccess.Rooms, PermissionAction.View);
        var rooms = await db.Rooms.AsNoTracking().Select(r => new { r.Id, r.BranchId }).ToListAsync(ct);
        var visible = rooms.Where(r => allowed is null || allowed.Contains(r.BranchId.Value)).Select(r => r.Id).ToHashSet();
        var query = db.RoomIssues.AsNoTracking();
        if (roomId is { } id) query = query.Where(i => i.RoomId == new RoomId(id));
        var issues = (await query.ToListAsync(ct)).Where(i => visible.Contains(i.RoomId)).OrderByDescending(i => i.ReportedAt).ToList();
        var who = await names.OfAsync(issues.Select(i => (Guid?)i.ReportedBy), ct);
        return issues.Select(i => ToDto(i, who)).ToList();
    }

    public async Task<RoomIssueDto> IssueAsync(RoomIssueId id, CancellationToken ct)
    {
        var issue = await db.RoomIssues.AsNoTracking().FirstAsync(i => i.Id == id, ct);
        return ToDto(issue, await names.OfAsync([issue.ReportedBy], ct));
    }

    /// <summary>Phòng → các lớp đang mở dùng phòng (chờ khai giảng / đang học / tạm dừng)</summary>
    public async Task<IReadOnlyDictionary<RoomId, List<OpenClass>>> OpenClassesAsync(CancellationToken ct)
    {
        var rows = await db.Classes.AsNoTracking()
            .Where(c => c.RoomId != null && (c.Status == ClassStatus.Enrolling || c.Status == ClassStatus.Ongoing || c.Status == ClassStatus.Paused))
            .Select(c => new { c.RoomId, c.Code, c.MaxStudents })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.RoomId!.Value).ToDictionary(g => g.Key, g => g.Select(r => new OpenClass(r.Code, r.MaxStudents)).ToList());
    }

    private static IQueryable<Row> Query(IQueryable<Room> source) => source.AsNoTracking().Select(r => new Row(
        r, EF.Property<uint>(r, "xmin"), EF.Property<DateTimeOffset>(r, "CreatedAt")));

    private static RoomDto ToDto(Row r, IReadOnlyDictionary<RoomId, List<OpenClass>> open)
    {
        var x = r.Room;
        return new RoomDto(x.Id.Value, x.BranchId.Value, x.Name, x.Floor, x.Type, x.Capacity, x.Area, x.Equipment, x.Status,
            x.MaintenanceReason, x.MaintenanceUntil, x.Note,
            open.TryGetValue(x.Id, out var list) ? list.Select(c => c.Code).Order().ToList() : [], r.CreatedAt, r.Version);
    }

    private static RoomIssueDto ToDto(RoomIssue i, IReadOnlyDictionary<Guid, string> who) => new(
        i.Id.Value, i.RoomId.Value, i.Equipment, i.Title, i.Description, i.Severity, i.Status, i.ReportedBy,
        who.GetValueOrDefault(i.ReportedBy) ?? "—", i.ReportedAt, i.Assignee, i.DueDate, i.StartedAt, i.ResolvedAt, i.Resolution, i.Cost?.Amount);

    public sealed record OpenClass(string Code, int MaxStudents);

    private sealed record Row(Room Room, uint Version, DateTimeOffset CreatedAt);
}
