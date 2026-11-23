using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Recruitment.Common;

/// <summary>
/// Tìm tin / ứng viên trong phạm vi <c>hr.recruitment</c> (theo chi nhánh của tin) và đọc ra DTO. Ngoài phạm vi → coi như không có (404).
/// </summary>
public sealed class RecruitmentDesk(IHrDbContext db, HrScope scope)
{
    public bool InScope(Job job, PermissionAction action) => scope.InBranch(HrScope.Recruitment, action, job.BranchId.Value);

    public async Task<Job?> FindJobAsync(Guid id, PermissionAction action, bool track, CancellationToken ct)
    {
        var query = track ? db.Jobs : db.Jobs.AsNoTracking();
        var job = await query.FirstOrDefaultAsync(j => j.Id == new JobId(id), ct);
        return job is not null && InScope(job, action) ? job : null;
    }

    /// <summary>Ứng viên (theo dõi để sửa) + tin của họ, trong phạm vi quyền</summary>
    public async Task<(Candidate Candidate, Job Job)?> FindCandidateAsync(Guid id, PermissionAction action, CancellationToken ct)
    {
        var c = await db.Candidates.FirstOrDefaultAsync(x => x.Id == new CandidateId(id), ct);
        if (c is null) return null;
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == c.JobId, ct);
        return job is not null && InScope(job, action) ? (c, job) : null;
    }

    public async Task<JobDto> JobDtoAsync(Job job, CancellationToken ct)
    {
        var stages = await db.Candidates.AsNoTracking().Where(c => c.JobId == job.Id).Select(c => c.Stage).ToListAsync(ct);
        return ToDto(job, stages);
    }

    public static JobDto ToDto(Job j, IReadOnlyCollection<CandidateStage> stages) => new(j.Id.Value, j.Title, j.BranchId.Value, j.Department, j.Kind,
        j.Quantity, j.SalaryRange, j.Status, j.PostedAt, j.Deadline, j.Channels, j.Description, stages.Count(s => s == CandidateStage.Hired),
        stages.Count(s => s == CandidateStage.Interview), stages.Count(s => s == CandidateStage.Offer), stages.Count);

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> employeeIds, CancellationToken ct)
    {
        var ids = employeeIds.Distinct().Select(x => new EmployeeId(x)).ToList();
        return await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id.Value, e => e.FullName, ct);
    }

    public static CandidateDto ToDto(Candidate c, IReadOnlyDictionary<Guid, string> names) => new(c.Id.Value, c.JobId.Value, c.FullName, c.Phone,
        c.Email, c.Source, c.Stage, c.AppliedAt, c.CvName, c.CvKey is not null, c.Japanese, c.ExpectedSalary?.Amount,
        c is { InterviewAt: { } at, InterviewerId: { } who, InterviewMode: { } mode }
            ? new InterviewDto(at, names.GetValueOrDefault(who, "?"), who, mode, c.InterviewScore, c.InterviewNote) : null,
        c is { OfferSalary: { } salary, OfferStartDate: { } start, OfferSentAt: { } sent } ? new OfferDto(salary.Amount, start, sent, c.OfferAccepted) : null,
        c.RejectReason, c.Notes.OrderBy(n => n.At).Select(n => new CandidateNoteDto(n.At, n.ByName, n.Text)).ToList());

    public async Task<CandidateResult> ResultAsync(Candidate c, Job job, Guid? onboardingId, IReadOnlyList<string> warnings, CancellationToken ct)
    {
        var names = await NamesAsync(c.InterviewerId is { } i ? [i] : [], ct);
        return new CandidateResult(ToDto(c, names), await JobDtoAsync(job, ct), onboardingId, warnings);
    }

    /// <summary>Kiểm số tiền VND (≥ 0, không lẻ) gửi lên</summary>
    public static Result<Money?> Vnd(decimal? amount)
    {
        if (amount is not { } a) return (Money?)null;
        var m = Money.Create(a);
        return m.IsFailure ? m.Error : m.Value;
    }
}
