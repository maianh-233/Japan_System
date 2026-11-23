using System.Globalization;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Seed;

/// <summary>
/// Dữ liệu mẫu giai đoạn 21 (user story 13) từ mock frontend (<c>npm run export-seed</c> → hr-ops.seed.json): chấm công, đơn từ, tuyển dụng, onboarding.
/// Mỗi nhóm chỉ nạp khi bảng của nhóm đó còn trống. Ngày trong mẫu tính theo ngày xuất file (mock sinh theo "hôm nay").
/// Phép năm KHÔNG trừ lại cho đơn nghỉ đã duyệt (số dư trong hr.seed.json đã là số còn lại).
/// </summary>
internal sealed class HrOpsSeeder(HrDbContext db, ILogger<HrOpsSeeder> logger) : IDataSeeder
{
    /// <summary>Sau nhân viên (HrSeeder = 10)</summary>
    public int Order => 15;

    public async Task SeedAsync(CancellationToken ct)
    {
        var data = SeedData.Read<HrOpsSeedFile>(typeof(HrOpsSeeder).Assembly, "hr-ops.seed.json");
        var counts = new List<string>();

        if (!await db.TimeRecords.AnyAsync(ct))
        {
            foreach (var r in data.TimeRecords) db.AddWithId(TimeRecordOf(r), SeedData.IdOf(r.Id));
            counts.Add($"{data.TimeRecords.Count} ngày công");
        }

        if (!await db.HrRequests.AnyAsync(ct))
        {
            var n = 0;
            foreach (var r in data.Requests)
            {
                if (RequestOf(r) is not { } request) continue;
                db.AddWithId(request, SeedData.IdOf(r.Id));
                db.SetCreatedAt(request, r.CreatedAt);
                n++;
            }
            counts.Add($"{n} đơn từ");
        }

        if (!await db.Jobs.IgnoreQueryFilters().AnyAsync(ct))
        {
            foreach (var j in data.Jobs)
            {
                var job = Job.Create(j.Title, Branch(j.BranchId), Parse<Department>(j.Department), Parse<EmployeeKind>(j.Kind), j.Quantity, j.SalaryRange,
                    DateOnly.Parse(j.PostedAt, CultureInfo.InvariantCulture), DateOnly.Parse(j.Deadline, CultureInfo.InvariantCulture), j.Channels, j.Description).Value;
                if (j.Status == "paused") job.Pause();
                if (j.Status == "closed") job.Close();
                db.AddWithId(job, SeedData.IdOf(j.Id));
            }
            foreach (var c in data.Candidates)
            {
                if (CandidateOf(c) is not { } candidate) continue;
                db.AddWithId(candidate, SeedData.IdOf(c.Id));
                db.SetCreatedAt(candidate, c.AppliedAt);
            }
            counts.Add($"{data.Jobs.Count} tin tuyển, {data.Candidates.Count} ứng viên");
        }

        if (!await db.Onboardings.AnyAsync(ct))
        {
            foreach (var o in data.Onboardings)
            {
                var kind = Parse<EmployeeKind>(o.Kind);
                var onboarding = Onboarding.Create(o.CandidateId is null ? null : new CandidateId(SeedData.IdOf(o.CandidateId)), o.FullName, o.Phone, o.Email,
                    o.Position, Parse<Department>(o.Department), kind, Branch(o.BranchId), DateOnly.Parse(o.StartDate, CultureInfo.InvariantCulture),
                    Employee(o.MentorId ?? o.Id)).Value;
                foreach (var i in o.Items.Where(i => i.Done))
                    onboarding.Tick(i.Key, true, i.DoneById is null ? Guid.Empty : SeedData.IdOf(i.DoneById), i.DoneBy ?? "", i.DoneAt ?? o.CreatedAt);
                if (o.EmployeeId is not null) onboarding.LinkEmployee(Employee(o.EmployeeId));
                db.AddWithId(onboarding, SeedData.IdOf(o.Id));
                db.SetCreatedAt(onboarding, o.CreatedAt);
            }
            counts.Add($"{data.Onboardings.Count} phiếu onboarding");
        }

        if (counts.Count == 0) return;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã nạp dữ liệu mẫu nhân sự mở rộng: {Counts}", string.Join(", ", counts));
    }

    private static TimeRecord TimeRecordOf(TimeRecordSeed r)
    {
        var date = DateOnly.Parse(r.Date, CultureInfo.InvariantCulture);
        var inAt = Time(r.CheckIn);
        var outAt = Time(r.CheckOut);
        if (r.EditReason is not null)
            return TimeRecord.Manual(Employee(r.EmployeeId), Branch(r.BranchId), date, inAt, outAt,
                r.EditedById is null ? Guid.Empty : SeedData.IdOf(r.EditedById), r.EditedByName ?? "", r.EditReason, date).Value;
        var record = TimeRecord.CheckInAt(Employee(r.EmployeeId), Branch(r.BranchId), date, inAt ?? new TimeOnly(8, 0));
        if (outAt is { } o) record.CheckOutAt(o);
        return record;
    }

    private HrRequest? RequestOf(RequestSeed r)
    {
        var type = Parse<HrRequestType>(r.Type);
        var from = DateOnly.Parse(r.From, CultureInfo.InvariantCulture);
        var details = new HrRequestDetails(
            PaidLeave: r.PaidLeave,
            Amount: r.Amount is { } a ? Money.FromTrusted(a) : null,
            Title: r.Title,
            SwapMyShiftId: r.Swap is null ? null : SeedData.IdOf(r.Swap.MyShiftId),
            SwapWithEmployeeId: r.Swap is null ? null : Employee(r.Swap.WithEmployeeId),
            SwapTheirShiftId: r.Swap is null ? null : SeedData.IdOf(r.Swap.TheirShiftId));
        var steps = r.Steps.Select(s => new PlannedStep(s.Role, s.ApproverId is null ? null : new ApproverRef(SeedData.IdOf(s.ApproverId), s.Approver))).ToList();
        // "Hôm nay" của mẫu = ngày đơn bắt đầu (đơn mẫu có thể đã qua > 30 ngày)
        var created = HrRequest.Create(r.Code, type, Employee(r.EmployeeId), Branch(r.BranchId), from, DateOnly.Parse(r.To, CultureInfo.InvariantCulture),
            r.Reason, details, steps, r.CreatedAt, from);
        if (created.IsFailure)
        {
            logger.LogWarning("Bỏ qua đơn mẫu {Id}: {Error}", r.Id, created.Error.Message);
            return null;
        }
        var request = created.Value;
        foreach (var s in r.Steps.Where(s => s.Decision is not null && s.ApproverId is not null))
            request.Decide(SeedData.IdOf(s.ApproverId!), s.Approver, canOverride: true, Parse<ApprovalDecision>(s.Decision!),
                s.Comment ?? (s.Decision == "rejected" ? "Không duyệt" : null), s.At ?? r.CreatedAt);
        if (r.Status == "cancelled") request.Cancel(request.EmployeeId);
        return request;
    }

    private Candidate? CandidateOf(CandidateSeed c)
    {
        var created = Candidate.Apply(new JobId(SeedData.IdOf(c.JobId)), c.FullName, c.Phone, c.Email, c.Source, c.AppliedAt, c.Japanese,
            c.ExpectedSalary is { } s ? Money.FromTrusted(s) : null);
        if (created.IsFailure)
        {
            logger.LogWarning("Bỏ qua ứng viên mẫu {Id}: {Error}", c.Id, created.Error.Message);
            return null;
        }
        var candidate = created.Value;
        if (c.CvName is not null) candidate.AttachCv($"hr/cv/demo/{c.CvName}", c.CvName);
        if (c.Stage != "applied" && c.Stage != "rejected" || c.Interview is not null) candidate.MoveTo(CandidateStage.Screening);
        if (c.Interview is { } iv)
            candidate.ScheduleInterview(iv.At, iv.InterviewerId is null ? Guid.Empty : SeedData.IdOf(iv.InterviewerId), Parse<InterviewMode>(iv.Mode), iv.Score, iv.Note);
        if (c.Offer is { } of)
        {
            var start = DateOnly.Parse(of.StartDate, CultureInfo.InvariantCulture);
            candidate.SendOffer(Money.FromTrusted(of.Salary), start, of.SentAt, start);
        }
        switch (c.Stage)
        {
            case "hired": candidate.Hire(); break;
            case "rejected": candidate.Reject(c.RejectReason ?? "Không phù hợp"); break;
        }
        foreach (var n in c.Notes) candidate.AddNote(n.Text, n.ById is null ? Guid.Empty : SeedData.IdOf(n.ById), n.By, n.At);
        return candidate;
    }

    private static TimeOnly? Time(string? hm) => hm is null ? null : TimeOnly.ParseExact(hm, "HH:mm", CultureInfo.InvariantCulture);
    private static EmployeeId Employee(string mockId) => new(SeedData.IdOf(mockId));
    private static BranchId Branch(string? mockId) => new(SeedData.IdOf(mockId ?? "cg"));
    private static T Parse<T>(string value) where T : struct, Enum => SnakeCaseEnumConverter<T>.Parse(value);

    private sealed record HrOpsSeedFile(List<TimeRecordSeed> TimeRecords, List<RequestSeed> Requests, List<JobSeed> Jobs, List<CandidateSeed> Candidates,
        List<OnboardingSeed> Onboardings);

    private sealed record TimeRecordSeed(string Id, string EmployeeId, string? BranchId, string Date, string? CheckIn, string? CheckOut, string? EditedById,
        string? EditedByName, string? EditReason);

    private sealed record RequestSeed(string Id, string Code, string Type, string EmployeeId, string? BranchId, string From, string To, bool? PaidLeave,
        SwapSeed? Swap, decimal? Amount, string? Title, string Reason, string Status, List<StepSeed> Steps, DateTimeOffset CreatedAt);

    private sealed record SwapSeed(string MyShiftId, string WithEmployeeId, string TheirShiftId);

    private sealed record StepSeed(string Role, string Approver, string? ApproverId, string? Decision, DateTimeOffset? At, string? Comment);

    private sealed record JobSeed(string Id, string Title, string BranchId, string Department, string Kind, int Quantity, string? SalaryRange, string Status,
        string PostedAt, string Deadline, List<string> Channels, string? Description);

    private sealed record CandidateSeed(string Id, string JobId, string FullName, string Phone, string Email, string Source, string Stage,
        DateTimeOffset AppliedAt, string? CvName, string? Japanese, decimal? ExpectedSalary, InterviewSeed? Interview, OfferSeed? Offer, string? RejectReason,
        List<NoteSeed> Notes);

    private sealed record InterviewSeed(DateTimeOffset At, string? InterviewerId, string Mode, decimal? Score, string? Note);

    private sealed record OfferSeed(decimal Salary, string StartDate, DateTimeOffset SentAt);

    private sealed record NoteSeed(DateTimeOffset At, string By, string? ById, string Text);

    private sealed record OnboardingSeed(string Id, string? CandidateId, string FullName, string Phone, string Email, string Position, string Department,
        string Kind, string BranchId, string StartDate, string? MentorId, List<ItemSeed> Items, string? EmployeeId, DateTimeOffset CreatedAt);

    private sealed record ItemSeed(string Key, bool Done, string? DoneBy, string? DoneById, DateTimeOffset? DoneAt);
}
