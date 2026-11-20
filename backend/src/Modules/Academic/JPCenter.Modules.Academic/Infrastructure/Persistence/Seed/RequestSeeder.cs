using System.Globalization;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp đơn hủy buổi, lời mời dạy thay, đơn xin nghỉ, đề xuất lên lớp, nhận xét cuối khóa mẫu từ <c>requests.seed.json</c> — sau lớp + buổi học.
/// Mỗi nhóm chỉ nạp khi bảng của nhóm còn trống (DB đã có lớp từ giai đoạn trước vẫn nhận thêm dữ liệu này).
/// Đơn nghỉ đã duyệt được ÁP lên buổi học bằng method domain: buổi gốc vắng có phép, buổi bù có HV khách + kết quả học bù
/// (<c>classes.seed.json</c> giữ điểm danh gốc, không chứa hiệu lực đơn).
/// Quy tắc "gửi trước giờ học" kiểm theo lúc gửi trong mock; mock lệch giờ (đơn tạo sau buổi) → coi như gửi trước giờ học 1 tiếng.
/// Buổi / lớp tham chiếu không có trong DB (dữ liệu mẫu lệch ngày) → bỏ qua dòng đó, ghi cảnh báo.
/// </summary>
internal sealed class RequestSeeder(AcademicDbContext db, IClock clock, ILogger<RequestSeeder> logger) : IDataSeeder
{
    public int Order => 32;

    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!await db.Classes.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<RequestSeedFile>(typeof(RequestSeeder).Assembly, "requests.seed.json");

        var sessionIds = data.CancelRequests.Select(x => x.SessionId)
            .Concat(data.SubInvites.Select(x => x.SessionId))
            .Concat(data.LeaveRequests.Select(x => x.SessionId))
            .Concat(data.LeaveRequests.Where(x => x.MakeupSessionId is not null).Select(x => x.MakeupSessionId!))
            .Distinct().Select(id => new ClassSessionId(SeedData.IdOf(id))).ToList();
        var sessions = await db.ClassSessions.IgnoreQueryFilters().Where(s => sessionIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var classIds = (await db.Classes.IgnoreQueryFilters().Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        ClassSession? Session(string id) => sessions.GetValueOrDefault(new ClassSessionId(SeedData.IdOf(id)));
        bool HasClass(string id) => classIds.Contains(new ClassId(SeedData.IdOf(id)));

        var counts = new int[5];
        if (!await db.SessionCancelRequests.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var r in data.CancelRequests)
                if (Session(r.SessionId) is { } s && CancelRequest(r, s) is { } x)
                {
                    db.AddWithId(x, SeedData.IdOf(r.Id));
                    counts[0]++;
                }
        }

        if (!await db.SubstituteInvites.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var i in data.SubInvites)
                if (Session(i.SessionId) is { } s && Invite(i, s) is { } x)
                {
                    db.AddWithId(x, SeedData.IdOf(i.Id));
                    counts[1]++;
                }
        }

        if (!await db.LeaveRequests.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var r in data.LeaveRequests)
                if (Session(r.SessionId) is { } s && Leave(r, s, r.MakeupSessionId is null ? null : Session(r.MakeupSessionId)))
                    counts[2]++;
        }

        var today = clock.Today;
        if (!await db.PromotionCandidates.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var p in data.Promotions.Where(p => HasClass(p.FromClassId) && (p.ToClassId is null || HasClass(p.ToClassId))))
                if (Promotion(p, today) is { } x)
                {
                    db.AddWithId(x, SeedData.IdOf(p.Id));
                    counts[3]++;
                }
        }

        if (!await db.StudentEvaluations.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var e in data.Evaluations.Where(e => HasClass(e.ClassId)))
            {
                var created = StudentEvaluation.Create(new ClassId(SeedData.IdOf(e.ClassId)), SeedData.IdOf(e.StudentId), SeedData.IdOf(e.TeacherId),
                    new EvaluationScores(e.Skills.Vocab, e.Skills.Grammar, e.Skills.Reading, e.Skills.Listening, e.Skills.Speaking, e.Attitude),
                    e.Strengths, e.Weaknesses, Enum<EvalRecommendation>(e.Recommendation), e.Advice, classEndedOn: null, today);
                if (Ok(created, e.Id)) { db.AddWithId(created.Value, SeedData.IdOf(e.Id)); counts[4]++; }
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã nạp {Cancel} đơn hủy buổi, {Invites} lời mời dạy thay, {Leave} đơn xin nghỉ, {Promo} đề xuất lên lớp, {Eval} nhận xét mẫu",
            counts[0], counts[1], counts[2], counts[3], counts[4]);
    }

    private SessionCancelRequest? CancelRequest(SeedCancel r, ClassSession s)
    {
        var at = At(r.CreatedAt);
        var created = SessionCancelRequest.Create(s.Id, s.ClassId, s.BranchId, SeedData.IdOf(r.TeacherId), r.ReasonType, r.Reason, AtOrNull(r.MakeupAt),
            s.StartsAt, SentBefore(at, s), at);
        if (!Ok(created, r.Id)) return null;
        var x = created.Value;
        var decided = At(r.DecidedAt ?? r.CreatedAt);
        var done = r.Status switch
        {
            "approved" => x.Approve(Person(r.DecidedById), decided, r.Note),
            "rejected" => x.Reject(Person(r.DecidedById), decided, r.Note ?? "Không duyệt"),
            _ => Result.Success(),
        };
        return Ok(done, r.Id) ? x : null;
    }

    private SubstituteInvite? Invite(SeedInvite i, ClassSession s)
    {
        var created = SubstituteInvite.Create(s.Id, s.ClassId, s.BranchId, null, SeedData.IdOf(i.FromTeacherId), SeedData.IdOf(i.ToTeacherId),
            i.Reason, Person(i.InvitedById), At(i.CreatedAt), At(i.RespondBy));
        if (!Ok(created, i.Id)) return null;
        var x = created.Value;
        var responded = At(i.RespondedAt ?? i.CreatedAt);
        var done = i.Status switch
        {
            "declined" => x.Decline(i.DeclineReason, responded),
            "accepted" => x.Accept(responded),
            "expired" => Expire(x, i.DeclineReason, responded),
            _ => Result.Success(),
        };
        return Ok(done, i.Id) ? x : null;

        static Result Expire(SubstituteInvite x, string? reason, DateTimeOffset at)
        {
            x.Expire(reason ?? "Quá hạn trả lời", at);
            return Result.Success();
        }
    }

    /// <summary>Tạo đơn + áp hiệu lực lên buổi gốc / buổi bù khi đã duyệt</summary>
    private bool Leave(SeedLeave r, ClassSession original, ClassSession? makeup)
    {
        var at = At(r.CreatedAt);
        var studentId = SeedData.IdOf(r.StudentId);
        var created = LeaveRequest.Create(studentId, original.ClassId, original.Id, original.BranchId, r.ReasonType, r.Reason,
            Enum<RequestedBy>(r.By), Person(r.ByAccountId), r.ByName, makeup?.Id, original.StartsAt, SentBefore(at, original), at);
        if (!Ok(created, r.Id)) return false;
        var x = db.AddWithId(created.Value, SeedData.IdOf(r.Id));

        var decided = At(r.DecidedAt ?? r.CreatedAt);
        if (r.Status == "rejected") return Ok(x.Reject(Person(r.DecidedById), decided, r.Note ?? "Không duyệt"), r.Id);
        if (r.Status != "approved") return true;
        if (!Ok(x.Approve(Person(r.DecidedById), decided, r.Note), r.Id)) return false;

        // Mock: HV có đơn đã duyệt thì buổi gốc ghi vắng có phép (điểm danh gốc trong classes.seed.json có thể là "có mặt")
        if (original.Attendance.Any(a => a.StudentId == studentId))
            original.RecordAttendance([new AttendanceEntry(studentId, AttendanceStatus.Excused, null, $"Có đơn xin nghỉ — {r.ReasonType}")],
                AttendanceMethod.Staff, Person(r.DecidedById), decided);
        original.MarkExcusedByLeave(studentId, x.Id.Value);
        if (makeup is null) return true;
        makeup.AddMakeupGuest(studentId, original.Id, x.Id.Value);
        if (r.MakeupResult is { } result)
        {
            var status = Enum<AttendanceStatus>(result);
            var attended = status is AttendanceStatus.Present or AttendanceStatus.Late;
            makeup.RecordAttendance([new AttendanceEntry(studentId, status, attended ? makeup.StartTime : null, null)],
                makeup.IsConfirmed ? AttendanceMethod.Staff : AttendanceMethod.Qr, Guid.Empty, decided);
            original.MarkMadeUp(studentId, makeup.Id, status);
            x.RecordMakeupResult(status);
        }
        return true;
    }

    private PromotionCandidate? Promotion(SeedPromotion p, DateOnly today)
    {
        var created = PromotionCandidate.Create(SeedData.IdOf(p.StudentId), new BranchId(SeedData.IdOf(p.BranchId)), new ClassId(SeedData.IdOf(p.FromClassId)),
            Enum<PromotionSource>(p.Source));
        if (!Ok(created, p.Id)) return null;
        var x = created.Value;
        if (p.ToClassId is not null) x.ChooseTarget(new ClassId(SeedData.IdOf(p.ToClassId)));
        if (p.SeatHeld && DateOnly.TryParse(p.HoldUntil, CultureInfo.InvariantCulture, out var until))
            Ok(x.HoldSeat(null, until < today ? today : until, today), p.Id);
        if (p.HandedOverAt is not null) x.HandOver(p.SaleId is null ? null : SeedData.IdOf(p.SaleId), At(p.HandedOverAt));
        if (p.Interest is { } i) x.MarkInterest(Enum<InterestBy>(i.By), At(i.At));
        foreach (var n in p.Notes) x.AddNote(n.Text, n.Channel, Person(n.ById), n.By, At(n.At));
        var done = p.Status switch
        {
            "unreachable" => x.MarkUnreachable(),
            "declined" => x.Decline(p.DeclineReason ?? "Không học tiếp"),
            _ => Result.Success(),
        };
        return Ok(done, p.Id) ? x : null;
    }

    /// <summary>Lúc gửi theo giờ VN; gửi sau giờ học (mock lệch) → 1 tiếng trước giờ học</summary>
    private static DateTime SentBefore(DateTimeOffset at, ClassSession s)
    {
        var local = at.ToOffset(Vietnam).DateTime;
        return local < s.StartsAt ? local : s.StartsAt.AddHours(-1);
    }

    private bool Ok(Result r, string id)
    {
        if (r.IsFailure) logger.LogWarning("Bỏ qua dữ liệu mẫu {Id}: {Error}", id, r.Error.Message);
        return r.IsSuccess;
    }

    private bool Ok<T>(Result<T> r, string id) => Ok((Result)r, id);

    private static Guid Person(string? mockId) => mockId is null ? Guid.Empty : SeedData.IdOf(mockId);

    private static T Enum<T>(string value) where T : struct, global::System.Enum => SnakeCaseEnumConverter<T>.Parse(value);

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture).ToUniversalTime();

    private static DateTimeOffset? AtOrNull(string? iso) => iso is null ? null : At(iso);

    private sealed record RequestSeedFile(List<SeedCancel> CancelRequests, List<SeedInvite> SubInvites, List<SeedLeave> LeaveRequests,
        List<SeedPromotion> Promotions, List<SeedEvaluation> Evaluations);

    private sealed record SeedCancel(string Id, string SessionId, string TeacherId, string ReasonType, string Reason, string? MakeupAt, string Status,
        string CreatedAt, string? DecidedById, string? DecidedAt, string? Note);

    private sealed record SeedInvite(string Id, string SessionId, string FromTeacherId, string ToTeacherId, string Reason, string? InvitedById,
        string CreatedAt, string RespondBy, string Status, string? RespondedAt, string? DeclineReason);

    private sealed record SeedLeave(string Id, string StudentId, string SessionId, string ReasonType, string Reason, string By, string ByName,
        string? ByAccountId, string Status, string CreatedAt, string? DecidedById, string? DecidedAt, string? MakeupSessionId, string? MakeupResult,
        string? Note);

    private sealed record SeedPromotion(string Id, string StudentId, string BranchId, string FromClassId, string? ToClassId, bool SeatHeld,
        string? HoldUntil, string Status, string Source, string? HandedOverAt, string? SaleId, SeedInterest? Interest, string? DeclineReason,
        List<SeedNote> Notes);

    private sealed record SeedInterest(string At, string By);

    private sealed record SeedNote(string At, string By, string? ById, string Text, string? Channel);

    private sealed record SeedEvaluation(string Id, string ClassId, string StudentId, string TeacherId, SeedSkills Skills, int Attitude,
        string? Strengths, string? Weaknesses, string Recommendation, string? Advice);

    private sealed record SeedSkills(int Vocab, int Grammar, int Reading, int Listening, int Speaking);
}
