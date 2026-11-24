using System.Globalization;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp kỳ thi + đăng ký JLPT (HV-16) và hồ sơ du học / XKLĐ (HV-17) từ <c>programs.seed.json</c> — sau học viên, mỗi nhóm chỉ khi bảng trống.
/// Đi qua method domain; chi nhánh lấy theo học viên. Mock có vài chỗ lệch quy tắc → chỉnh cho khớp:
/// lượt đã nộp hội đồng mà thiếu giấy tờ → coi như đủ; hồ sơ du học đã qua bước Giấy tờ / Phỏng vấn → giấy tờ kiểm tra hết / đỗ phỏng vấn.
/// HV trùng kỳ thi hoặc không có trong DB → bỏ qua.
/// </summary>
internal sealed class ProgramsSeeder(StudentsDbContext db, ILogger<ProgramsSeeder> logger) : IDataSeeder
{
    public int Order => 42;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!await db.Students.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<ProgramsSeedFile>(typeof(ProgramsSeeder).Assembly, "programs.seed.json");
        var branches = await db.Students.IgnoreQueryFilters().ToDictionaryAsync(s => s.Id, s => s.BranchId, ct);
        (StudentId Id, BranchId Branch)? Student(string mockId) =>
            branches.TryGetValue(new StudentId(SeedData.IdOf(mockId)), out var b) ? (new StudentId(SeedData.IdOf(mockId)), b) : null;

        int sessions = 0, regs = 0, cases = 0;
        if (!await db.JlptSessions.AnyAsync(ct))
        {
            var created = new Dictionary<string, JlptSession>();
            foreach (var s in data.JlptSessions)
            {
                var fees = s.Fees.ToDictionary(kv => SnakeCaseEnumConverter<JlptLevel>.Parse(kv.Key), kv => Money.FromTrusted(kv.Value));
                var r = JlptSession.Create(s.Label, Date(s.ExamDate), Date(s.RegClose), Date(s.ResultDate), fees, s.Venues);
                if (r.IsFailure)
                {
                    logger.LogWarning("Bỏ qua kỳ thi JLPT mẫu {Id}: {Error}", s.Id, r.Error.Message);
                    continue;
                }
                db.AddWithId(r.Value, SeedData.IdOf(s.Id));
                created[s.Id] = r.Value;
                sessions++;
            }

            if (!await db.JlptRegistrations.IgnoreQueryFilters().AnyAsync(ct))
            {
                var taken = new HashSet<(string, StudentId)>();
                foreach (var x in data.JlptRegistrations)
                {
                    if (Student(x.StudentId) is not { } st || !created.TryGetValue(x.SessionId, out var session)) continue;
                    if (x.Status != "cancelled" && !taken.Add((x.SessionId, st.Id))) continue;
                    var venue = session.Venues.Contains(x.Venue ?? "") ? x.Venue : session.Venues[0];
                    var reg = JlptRegistration.Create(st.Id, session, st.Branch, SnakeCaseEnumConverter<JlptLevel>.Parse(x.Level), venue);
                    if (reg.IsFailure)
                    {
                        logger.LogWarning("Bỏ qua đăng ký JLPT mẫu {Id}: {Error}", x.Id, reg.Error.Message);
                        continue;
                    }
                    var r = reg.Value;
                    var step = Array.IndexOf(Flow, x.Status is "done" or "absent" ? "admit" : x.Status);
                    var docsOk = x.Docs.Photo && x.Docs.IdCard && x.Docs.Form;
                    r.SetDocuments(x.Docs.Photo || step >= 2, x.Docs.IdCard || step >= 2, x.Docs.Form || step >= 2);
                    var paidAt = x.PaidAt is null ? At(x.CreatedAt) : At(x.PaidAt);
                    var done = Result.Success();
                    if (step >= 1) done = r.MarkPaid(paidAt);
                    if (done.IsSuccess && step >= 2) done = r.MarkSubmitted();
                    if (done.IsSuccess && step >= 3) done = r.SetAdmit(x.AdmitNo ?? $"{x.Level}-{x.Id}");
                    if (done.IsSuccess && x.Status == "done") done = r.RecordResult(x.Scores ?? []);
                    if (done.IsSuccess && x.Status == "absent") done = r.MarkAbsent();
                    if (done.IsSuccess && x.Status == "cancelled") done = r.Cancel(x.Note);
                    if (done.IsFailure) logger.LogWarning("Đăng ký JLPT mẫu {Id}: {Error}", x.Id, done.Error.Message);
                    if (x.Status != "cancelled") r.SetNote(x.Note);
                    if (!docsOk && step >= 2) logger.LogDebug("Đăng ký JLPT mẫu {Id} đã nộp hội đồng nhưng thiếu giấy tờ — coi như đủ", x.Id);
                    db.AddWithId(r, SeedData.IdOf(x.Id));
                    db.SetCreatedAt(r, At(x.CreatedAt));
                    regs++;
                }
            }
        }

        if (!await db.AbroadCases.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var c in data.AbroadCases)
            {
                if (Student(c.StudentId) is not { } st || c.AssigneeId is null) continue;
                var info = new AbroadCaseInfo(SnakeCaseEnumConverter<AbroadProgram>.Parse(c.Program), c.Partner, c.Destination, c.Job, c.Intake,
                    c.JlptRequired is null ? null : SnakeCaseEnumConverter<JlptLevel>.Parse(c.JlptRequired), Money.FromTrusted(c.Fee.Total),
                    SeedData.IdOf(c.AssigneeId));
                var created = AbroadCase.Create(st.Id, st.Branch, info, c.Docs.Select(d => d.Name));
                if (created.IsFailure)
                {
                    logger.LogWarning("Bỏ qua hồ sơ du học mẫu {Id}: {Error}", c.Id, created.Error.Message);
                    continue;
                }
                var x = created.Value;
                var target = SnakeCaseEnumConverter<AbroadStage>.Parse(c.Stage);
                var byName = c.Assignee;
                var at = At(c.CreatedAt);
                for (var i = 0; i < x.Documents.Count; i++)
                {
                    var state = target > AbroadStage.Documents ? DocumentState.Verified : SnakeCaseEnumConverter<DocumentState>.Parse(c.Docs[i].State);
                    x.SetDocumentState(x.Documents[i].Id, state);
                }
                InterviewResult? interview = c.Interview?.Result is { } ir ? SnakeCaseEnumConverter<InterviewResult>.Parse(ir) : null;
                if (target > AbroadStage.Interview) interview = InterviewResult.Pass;
                if (c.Interview is not null) x.RecordInterview(Date(c.Interview.Date), interview);
                x.RecordCoe(DateOrNull(c.Coe?.SubmittedAt), DateOrNull(c.Coe?.GrantedAt));
                x.RecordVisa(DateOrNull(c.Visa?.AppliedAt), DateOrNull(c.Visa?.IssuedAt));
                x.SetFlight(DateOrNull(c.FlightDate));
                if (c.Fee.Paid > 0) x.RecordPayment(Money.FromTrusted(c.Fee.Paid));
                while (x.Stage < target)
                {
                    var moved = x.Advance(x.AssigneeId, byName, at);
                    if (moved.IsFailure)
                    {
                        logger.LogWarning("Hồ sơ du học mẫu {Id} dừng ở bước {Stage}: {Error}", c.Id, x.Stage, moved.Error.Message);
                        break;
                    }
                }
                foreach (var n in c.Notes) x.AddNote(n.Text, n.ById is null ? x.AssigneeId : SeedData.IdOf(n.ById), n.By, At(n.At));
                if (c.Cancelled is not null) x.Cancel(c.Cancelled.Reason, x.AssigneeId, byName, At(c.Cancelled.At));
                db.AddWithId(x, SeedData.IdOf(c.Id));
                db.SetCreatedAt(x, at);
                cases++;
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã nạp {Sessions} kỳ thi JLPT, {Regs} lượt đăng ký, {Cases} hồ sơ du học / XKLĐ mẫu", sessions, regs, cases);
    }

    /// <summary>Thứ tự tiến độ đăng ký JLPT trong mock</summary>
    private static readonly string[] Flow = ["registered", "paid", "submitted", "admit"];

    private static DateOnly Date(string s) => DateOnly.Parse(s[..10], CultureInfo.InvariantCulture);
    private static DateOnly? DateOrNull(string? s) => s is null ? null : Date(s);
    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture).ToUniversalTime();

    private sealed record ProgramsSeedFile(List<SessionSeed> JlptSessions, List<RegSeed> JlptRegistrations, List<AbroadSeed> AbroadCases);

    private sealed record SessionSeed(string Id, string Label, string ExamDate, string RegClose, string ResultDate, Dictionary<string, decimal> Fees,
        List<string> Venues);

    private sealed record DocsSeed(bool Photo, bool IdCard, bool Form);

    private sealed record RegSeed(string Id, string StudentId, string SessionId, string Level, string? Venue, string Status, DocsSeed Docs,
        string? PaidAt, string? AdmitNo, List<int>? Scores, string? Note, string CreatedAt);

    private sealed record AbroadDocSeed(string Name, string State);

    private sealed record InterviewSeed(string Date, string? Result);

    private sealed record CoeSeed(string? SubmittedAt, string? GrantedAt);

    private sealed record VisaSeed(string? AppliedAt, string? IssuedAt);

    private sealed record FeeSeed(decimal Total, decimal Paid);

    private sealed record NoteSeed(string At, string By, string? ById, string Text);

    private sealed record CancelSeed(string Reason, string At);

    private sealed record AbroadSeed(string Id, string StudentId, string Program, string Partner, string Destination, string? Job, string Intake,
        string? JlptRequired, string Stage, CancelSeed? Cancelled, List<AbroadDocSeed> Docs, InterviewSeed? Interview, CoeSeed? Coe, VisaSeed? Visa,
        string? FlightDate, FeeSeed Fee, string Assignee, string? AssigneeId, List<NoteSeed> Notes, string CreatedAt);
}
