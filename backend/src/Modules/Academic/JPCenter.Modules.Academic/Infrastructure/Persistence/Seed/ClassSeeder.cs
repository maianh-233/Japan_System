using System.Globalization;
using System.Security.Cryptography;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp phòng, lớp, buổi học mẫu từ <c>classes.seed.json</c> (xuất từ mock frontend) — chạy khi bảng phòng còn trống, sau khóa học + lộ trình.
/// Đi qua method domain (khai giảng, hủy, điểm danh, xác nhận...) để dữ liệu mẫu đúng quy tắc; giờ "hiện tại" của các bước lấy theo buổi.
/// Mock ghi người thao tác bằng tên — tên không phải nhân viên (lễ tân chung...) → <see cref="Guid.Empty"/>.
/// Loại GV của lớp suy từ tiền tố mã (mock có lớp mã GVN_ thuộc khóa GV Việt); không kiểm sức chứa phòng (mock cố ý có lớp vượt sức chứa).
/// </summary>
internal sealed class ClassSeeder(AcademicDbContext db, ILogger<ClassSeeder> logger) : IDataSeeder
{
    public int Order => 31;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Rooms.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<ClassSeedFile>(typeof(ClassSeeder).Assembly, "classes.seed.json");
        var content = await ContentMapAsync(ct);

        var roomIds = new Dictionary<(string Branch, string Name), RoomId>();
        foreach (var s in data.Rooms)
        {
            var room = Room.Create(Branch(s.BranchId), s.Name, s.Floor, Enum<RoomType>(s.Type), s.Capacity, s.Area,
                s.Equipment.Select(Enum<Equipment>).ToList(), s.Note).Value;
            if (s.Status == "maintenance") room.StartMaintenance(s.MaintenanceReason ?? "Bảo trì", Date(s.MaintenanceUntil));
            if (s.Status == "inactive") room.Deactivate(hasOpenClasses: false);
            db.AddWithId(room, SeedData.IdOf(s.Id));
            roomIds[(s.BranchId, s.Name)] = room.Id;
        }

        foreach (var s in data.RoomIssues)
        {
            var issue = RoomIssue.Report(new RoomId(SeedData.IdOf(s.RoomId)), s.Equipment is null ? null : Enum<Equipment>(s.Equipment), s.Title,
                s.Description, Enum<IssueSeverity>(s.Severity), Person(s.ReportedById), At(s.ReportedAt)!.Value);
            if (s.Status is "in_progress" or "resolved")
                issue.Start(s.Assignee ?? "—", Date(s.DueDate), At(s.StartedAt ?? s.ReportedAt)!.Value);
            if (s.Status == "resolved")
                issue.Resolve(s.Resolution ?? "", s.Cost is null ? null : Money.FromTrusted(s.Cost.Value), At(s.ResolvedAt ?? s.ReportedAt)!.Value);
            db.AddWithId(issue, SeedData.IdOf(s.Id));
        }

        var seeded = new HashSet<string>();
        foreach (var s in data.Classes)
        {
            var online = s.Online ?? false;
            var setup = new ClassSetup(s.Code, s.Name, Branch(s.BranchId), s.TeacherIds.Select(SeedData.IdOf).ToList(),
                (s.AssistantIds ?? []).Select(SeedData.IdOf).ToList(),
                !online && s.Room is not null && roomIds.TryGetValue((s.BranchId, s.Room), out var room) ? room : null, online, s.MeetingUrl,
                DateOnly.Parse(s.PlannedStartDate, CultureInfo.InvariantCulture), DateOnly.Parse(s.PlannedEndDate, CultureInfo.InvariantCulture),
                s.Weekdays, Time(s.StartTime)!.Value, Time(s.EndTime)!.Value, s.TotalHours, s.MinStudents, s.MaxStudents,
                Money.FromTrusted(s.Fee), s.PreviousClassId is null ? null : new ClassId(SeedData.IdOf(s.PreviousClassId)), s.Note);
            var created = Class.Create(new CourseId(SeedData.IdOf(s.CourseId)),
                s.CurriculumId is null ? null : new CurriculumId(SeedData.IdOf(s.CurriculumId)), TypeOfCode(s.Code), setup, roomCapacity: null);
            if (created.IsFailure)
            {
                logger.LogWarning("Bỏ qua lớp mẫu {Code}: {Error}", s.Code, created.Error.Message);
                continue;
            }
            var c = created.Value;
            var start = Date(s.ActualStartDate) ?? setup.PlannedStartDate;
            if (s.Status is "ongoing" or "paused" or "finished") c.Start(start, enrolled: null);
            if (s.Status == "paused") c.Pause(s.Note ?? "Tạm dừng");
            if (s.Status == "finished") c.Finish(Date(s.ActualEndDate) ?? setup.PlannedEndDate);
            if (s.Status == "cancelled") c.Cancel(s.CancelReason ?? s.Note ?? "Hủy lớp");
            db.AddWithId(c, SeedData.IdOf(s.Id));
            seeded.Add(s.Id);
        }

        var branchOfClass = data.Classes.ToDictionary(c => c.Id, c => c.BranchId);
        foreach (var s in data.Sessions.Where(x => seeded.Contains(x.ClassId)))
        {
            var session = Session(s, Branch(branchOfClass[s.ClassId]), content);
            if (session is not null) db.AddWithId(session, SeedData.IdOf(s.Id));
        }

        await db.SaveChangesAsync(ct);
    }

    private ClassSession? Session(SeedSession s, BranchId branch, ContentMap content)
    {
        var date = DateOnly.Parse(s.Date, CultureInfo.InvariantCulture);
        var kind = Enum<SessionKind>(s.Kind);
        var origin = s.Status == "makeup" && s.MakeupForId is not null ? SessionOrigin.Makeup : s.Origin is "extra" ? SessionOrigin.Extra : SessionOrigin.Regular;
        var created = ClassSession.Create(new ClassId(SeedData.IdOf(s.ClassId)), branch, s.No, date, Time(s.StartTime)!.Value, Time(s.EndTime)!.Value,
            kind, s.LessonId is not null && content.Lessons.TryGetValue(s.LessonId, out var lesson) ? lesson : null,
            s.ExamId is not null && content.Exams.TryGetValue(s.ExamId, out var exam) ? exam : null, origin,
            origin == SessionOrigin.Makeup ? new ClassSessionId(SeedData.IdOf(s.MakeupForId!)) : null, NewQrToken());
        if (created.IsFailure)
        {
            logger.LogWarning("Bỏ qua buổi mẫu {Id}: {Error}", s.Id, created.Error.Message);
            return null;
        }
        var x = created.Value;
        var before = x.StartsAt.AddMinutes(-1);
        var after = x.EndsAt.AddMinutes(1);
        var dayAt = new DateTimeOffset(x.StartsAt, TimeSpan.FromHours(7)).ToUniversalTime();

        if (s.Status == "cancelled")
        {
            x.Cancel(s.Note ?? "Hủy buổi", Person(s.CancelledById), dayAt.AddDays(-1), before);
            return x;
        }

        foreach (var group in (s.Records ?? []).GroupBy(r => r.Method ?? "staff"))
            x.RecordAttendance(group.Select(r => new AttendanceEntry(SeedData.IdOf(r.StudentId), Enum<AttendanceStatus>(r.Status), Time(r.CheckInAt),
                r.Note, Time(r.LeftAt), r.LeaveReason)).ToList(), Enum<AttendanceMethod>(group.Key), Person(s.AttendanceById), dayAt);

        foreach (var t in s.TeacherAttendance ?? [])
            x.RecordStaffAttendance(SeedData.IdOf(t.TeacherId), Enum<StaffRole>(t.Role ?? "teacher"), Enum<CheckSource>(t.Source ?? "staff"),
                Enum<TeacherAttendanceStatus>(t.Status), Time(t.CheckIn), Time(t.CheckOut),
                t.SubstituteId is null ? null : SeedData.IdOf(t.SubstituteId), t.Note, At(t.CheckedAt) ?? dayAt);

        foreach (var e in s.SupportLog ?? [])
            x.AddSupport(Enum<SupportKind>(e.Kind), e.Note, Person(e.ById), At(e.At) ?? dayAt);

        if (s.Incident is { } i)
        {
            x.ReportIncident(new SessionIncident(Time(i.At)!.Value, Enum<IncidentReason>(i.Reason), i.Detail, Enum<IncidentAction>(i.Action),
                i.TakeoverId is null ? null : SeedData.IdOf(i.TakeoverId), i.Notified ?? false, Person(i.ReportedById), At(i.ReportedAt) ?? dayAt), after);
            if (i.Outcome is { } o)
                x.ResolveIncident(Enum<IncidentOutcome>(o.Kind), At(o.At) ?? dayAt, o.TeacherId is null ? null : SeedData.IdOf(o.TeacherId), Person(i.OutcomeById));
        }

        if (s.TeacherLog is { } log)
            x.SubmitReport(log.Content, log.Homework, log.Issues, log.Coverage is null ? LessonCoverage.Full : Enum<LessonCoverage>(log.Coverage), [],
                Person(log.AuthorId), At(log.EndedAt ?? log.At) ?? dayAt);

        if (s.Confirmation is { } cf) x.Confirm(Person(s.ConfirmedById), At(cf.At) ?? dayAt, cf.Note, after);
        return x;
    }

    /// <summary>Id bài / mốc thi mock → Id đã seed: theo thứ tự bài và tên mốc thi trong lộ trình</summary>
    private async Task<ContentMap> ContentMapAsync(CancellationToken ct)
    {
        var file = SeedData.Read<CurriculaFile>(typeof(ClassSeeder).Assembly, "academic.seed.json");
        var curricula = await db.Curricula.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var lessons = new Dictionary<string, LessonId>();
        var exams = new Dictionary<string, ExamMilestoneId>();
        foreach (var fc in file.Curricula)
        {
            if (!curricula.TryGetValue(new CurriculumId(SeedData.IdOf(fc.Id)), out var cur)) continue;
            var ordered = cur.Lessons.OrderBy(l => l.Order).ToList();
            for (var k = 0; k < fc.Lessons.Count && k < ordered.Count; k++) lessons[fc.Lessons[k].Id] = ordered[k].Id;
            foreach (var fe in fc.Exams)
                if (cur.Exams.FirstOrDefault(e => e.Title == fe.Title) is { } e) exams[fe.Id] = e.Id;
        }
        return new ContentMap(lessons, exams);
    }

    private static TeacherType TypeOfCode(string code) =>
        code.StartsWith("GVVN_", StringComparison.OrdinalIgnoreCase) ? TeacherType.Mixed
        : code.StartsWith("GVN_", StringComparison.OrdinalIgnoreCase) ? TeacherType.Jp : TeacherType.Vn;

    private static string NewQrToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static BranchId Branch(string mockId) => new(SeedData.IdOf(mockId));

    private static Guid Person(string? mockId) => mockId is null ? Guid.Empty : SeedData.IdOf(mockId);

    private static T Enum<T>(string value) where T : struct, global::System.Enum => SnakeCaseEnumConverter<T>.Parse(value);

    private static TimeOnly? Time(string? hhmm) => hhmm is null ? null : TimeOnly.Parse(hhmm, CultureInfo.InvariantCulture);

    private static DateOnly? Date(string? value) => value is null ? null : DateOnly.Parse(value[..10], CultureInfo.InvariantCulture);

    private static DateTimeOffset? At(string? iso) => iso is null ? null : DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture).ToUniversalTime();

    private sealed record ContentMap(Dictionary<string, LessonId> Lessons, Dictionary<string, ExamMilestoneId> Exams);

    private sealed record CurriculaFile(List<CurriculumRef> Curricula);

    private sealed record CurriculumRef(string Id, List<ItemRef> Lessons, List<ExamRef> Exams);

    private sealed record ItemRef(string Id);

    private sealed record ExamRef(string Id, string Title);

    private sealed record ClassSeedFile(List<SeedRoom> Rooms, List<SeedIssue> RoomIssues, List<SeedClass> Classes, List<SeedSession> Sessions);

    private sealed record SeedRoom(string Id, string BranchId, string Name, string? Floor, string Type, int Capacity, decimal? Area,
        List<string> Equipment, string Status, string? MaintenanceReason, string? MaintenanceUntil, string? Note);

    private sealed record SeedIssue(string Id, string RoomId, string? Equipment, string Title, string? Description, string Severity, string Status,
        string? ReportedById, string ReportedAt, string? Assignee, string? DueDate, string? StartedAt, string? ResolvedAt, string? Resolution,
        decimal? Cost);

    private sealed record SeedClass(string Id, string CourseId, string? CurriculumId, string Code, string Name, string BranchId,
        List<string> TeacherIds, List<string>? AssistantIds, string? Room, bool? Online, string? MeetingUrl, string PlannedStartDate,
        string PlannedEndDate, string? ActualStartDate, string? ActualEndDate, List<int> Weekdays, string StartTime, string EndTime,
        decimal TotalHours, int MinStudents, int MaxStudents, decimal Fee, string Status, string? PreviousClassId, string? CancelReason,
        string? Note);

    private sealed record SeedSession(string Id, string ClassId, int No, string Date, string StartTime, string EndTime, string Kind,
        string? LessonId, string? ExamId, string Status, string? Origin, string? MakeupForId, string? Note, List<SeedRecord>? Records,
        string? AttendanceById, List<SeedTeacher>? TeacherAttendance, List<SeedSupport>? SupportLog, SeedIncident? Incident,
        SeedLog? TeacherLog, SeedConfirmation? Confirmation, string? ConfirmedById, string? CancelledById);

    private sealed record SeedRecord(string StudentId, string Status, string? Note, string? CheckInAt, string? Method, string? LeftAt,
        string? LeaveReason);

    private sealed record SeedTeacher(string TeacherId, string? Role, string? Source, string? CheckedAt, string Status, string? CheckIn,
        string? CheckOut, string? SubstituteId, string? Note);

    private sealed record SeedSupport(string Kind, string Note, string? ById, string? At);

    private sealed record SeedIncident(string At, string Reason, string? Detail, string Action, string? TakeoverId, bool? Notified,
        string? ReportedById, string? ReportedAt, SeedOutcome? Outcome, string? OutcomeById);

    private sealed record SeedOutcome(string Kind, string? At, string? TeacherId);

    private sealed record SeedLog(string Content, string? Homework, string? Issues, string? Coverage, string? AuthorId, string? At, string? EndedAt);

    private sealed record SeedConfirmation(string? At, string? Note);
}
