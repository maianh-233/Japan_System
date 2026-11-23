using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.Modules.Work.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Hr;

/// <summary>Giai đoạn 21 phase 2 — dữ liệu mẫu chấm công, đơn từ, tuyển dụng, onboarding (Hr) + task, ca, bàn giao (Work) nạp đúng từ mock frontend.</summary>
[Collection(SeededCollection.Name)]
public sealed class HrWorkSeedTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonElement HrSeed = SeedData.Read<JsonElement>(typeof(HrDbContext).Assembly, "hr-ops.seed.json");
    private static readonly JsonElement WorkSeed = SeedData.Read<JsonElement>(typeof(WorkDbContext).Assembly, "work.seed.json");

    private static List<JsonElement> Items(JsonElement root, string name) => root.GetProperty(name).EnumerateArray().ToList();
    private static Guid Id(JsonElement e) => SeedData.IdOf(e.GetProperty("id").GetString()!);

    [Fact]
    public async Task Cham_cong_va_don_tu()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();

        var mockRecords = Items(HrSeed, "timeRecords");
        var recordIds = mockRecords.Select(r => new TimeRecordId(Id(r))).ToList();
        var records = await db.TimeRecords.AsNoTracking().Where(r => recordIds.Contains(r.Id)).ToListAsync(Ct);
        records.Count.ShouldBe(mockRecords.Count);
        records.ShouldAllBe(r => r.CheckIn != null && (r.CheckOut == null || r.CheckOut >= r.CheckIn));
        records.ShouldContain(r => r.EditReason != null && r.EditedByName != null);

        var mockRequests = Items(HrSeed, "requests");
        var requestIds = mockRequests.Select(r => new HrRequestId(Id(r))).ToList();
        var requests = await db.HrRequests.AsNoTracking().Where(r => requestIds.Contains(r.Id)).ToListAsync(Ct);
        requests.Count.ShouldBe(mockRequests.Count);
        foreach (var m in mockRequests)
        {
            var r = requests.Single(x => x.Id.Value == Id(m));
            r.Status.ToString().ToLowerInvariant().ShouldBe(m.GetProperty("status").GetString());
            r.Steps.Count.ShouldBe(m.GetProperty("steps").GetArrayLength());
        }
        // 2 cấp: bước 1 đã duyệt, đơn còn chờ bước 2
        requests.ShouldContain(r => r.Status == HrRequestStatus.Pending && r.Steps.Count == 2 && r.Steps[0].Decision == ApprovalDecision.Approved);
        requests.Single(r => r.Type == HrRequestType.ShiftSwap).SwapTheirShiftId.ShouldNotBeNull();
        requests.Where(r => r.Status == HrRequestStatus.Rejected).ShouldAllBe(r => r.Steps.Any(s => s.Comment != null));
    }

    [Fact]
    public async Task Tuyen_dung_va_onboarding()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();

        var mockJobs = Items(HrSeed, "jobs");
        var jobIds = mockJobs.Select(j => new JobId(Id(j))).ToList();
        var jobs = await db.Jobs.AsNoTracking().Where(j => jobIds.Contains(j.Id)).ToListAsync(Ct);
        jobs.Count.ShouldBe(mockJobs.Count);
        jobs.ShouldContain(j => j.Status == JobStatus.Paused);

        var mockCands = Items(HrSeed, "candidates");
        var candIds = mockCands.Select(c => new CandidateId(Id(c))).ToList();
        var cands = await db.Candidates.AsNoTracking().Where(c => candIds.Contains(c.Id)).ToListAsync(Ct);
        cands.Count.ShouldBe(mockCands.Count);
        foreach (var m in mockCands)
            cands.Single(c => c.Id.Value == Id(m)).Stage.ToString().ToLowerInvariant().ShouldBe(m.GetProperty("stage").GetString());
        cands.Where(c => c.Stage == CandidateStage.Offer).ShouldAllBe(c => c.OfferSalary != null && c.InterviewAt != null);
        cands.ShouldContain(c => c.Notes.Count > 0);

        var mockObs = Items(HrSeed, "onboardings");
        var obIds = mockObs.Select(o => new OnboardingId(Id(o))).ToList();
        var obs = await db.Onboardings.AsNoTracking().Where(o => obIds.Contains(o.Id)).ToListAsync(Ct);
        obs.Count.ShouldBe(mockObs.Count);
        obs.ShouldContain(o => o.CandidateId != null && o.EmployeeId == null && o.Items.Count(i => i.Done) == 3);
        obs.ShouldContain(o => o.EmployeeId != null && o.Progress == 100);
    }

    [Fact]
    public async Task Task_ca_va_ban_giao()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<WorkDbContext>();

        var mockShifts = Items(WorkSeed, "shifts");
        var shiftIds = mockShifts.Select(s => new ShiftAssignmentId(Id(s))).ToList();
        (await db.ShiftAssignments.CountAsync(s => shiftIds.Contains(s.Id), Ct)).ShouldBe(mockShifts.Count);
        (await db.ShiftRegistrations.CountAsync(r => r.Kind == ShiftRegistrationKind.Busy, Ct)).ShouldBeGreaterThan(0);

        var mockTasks = Items(WorkSeed, "tasks");
        var taskIds = mockTasks.Select(t => new WorkTaskId(Id(t))).ToList();
        var tasks = await db.Tasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).ToListAsync(Ct);
        tasks.Count.ShouldBe(mockTasks.Count);
        tasks.Where(t => t.Status == WorkTaskStatus.Done).ShouldAllBe(t => t.CompletedAt != null);
        tasks.ShouldContain(t => t.LinkType == TaskLinkType.Lead && t.Comments.Count > 0);
        tasks.ShouldContain(t => t.Checklist.Any(c => c.Done) && t.Checklist.Any(c => !c.Done));

        var mockHandovers = Items(WorkSeed, "handovers");
        var hoIds = mockHandovers.Select(h => new HandoverId(Id(h))).ToList();
        var hos = await db.Handovers.AsNoTracking().Where(h => hoIds.Contains(h.Id)).ToListAsync(Ct);
        hos.Count.ShouldBe(mockHandovers.Count);
        foreach (var m in mockHandovers)
            hos.Single(h => h.Id.Value == Id(m)).Status.ToString().ToLowerInvariant().ShouldBe(m.GetProperty("status").GetString());
        hos.ShouldContain(h => h.PendingItems.Any(i => i.Done));
    }
}
