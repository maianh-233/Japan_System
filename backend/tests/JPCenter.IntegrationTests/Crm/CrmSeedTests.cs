using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.Modules.Crm.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Crm;

/// <summary>Giai đoạn 18 phase 2 — dữ liệu mẫu Tuyển sinh nạp đúng từ mock frontend (user story 05).</summary>
[Collection(SeededCollection.Name)]
public sealed class CrmSeedTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonElement Seed = SeedData.Read<JsonElement>(typeof(CrmDbContext).Assembly, "crm.seed.json");

    private static List<JsonElement> Items(string name) => Seed.GetProperty(name).EnumerateArray().ToList();

    private static Guid Id(JsonElement e) => SeedData.IdOf(e.GetProperty("id").GetString()!);

    [Fact]
    public async Task Lead_lich_su_tu_van_lien_ket_chien_dich()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var mock = Items("leads");
        var ids = mock.Select(l => new LeadId(Id(l))).ToList();
        var leads = await db.Leads.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ToListAsync(Ct);

        leads.Count.ShouldBe(mock.Count);
        foreach (var m in mock)
        {
            var lead = leads.Single(l => l.Id == new LeadId(Id(m)));
            lead.Status.ShouldBe(SnakeCase<LeadStatus>(m.GetProperty("status").GetString()!), lead.Code);
            lead.Hidden.ShouldBe(m.GetProperty("hidden").GetBoolean());
            if (lead.Status == LeadStatus.Won) lead.StudentLinks.ShouldNotBeEmpty();
            if (lead.Status == LeadStatus.Lost) lead.LostReason.ShouldNotBeNullOrWhiteSpace();
            if (m.TryGetProperty("campaignId", out var c) && c.ValueKind == JsonValueKind.String)
                lead.CampaignId.ShouldBe(new CampaignId(SeedData.IdOf(c.GetString()!)));
        }

        // Lịch sử tư vấn mock (không tính dòng hệ thống: liên kết HV, ghi danh)
        leads.Sum(l => l.Consultations.Count(c => !c.IsSystem)).ShouldBe(Items("consultations").Count);
        leads.Where(l => l.Consultations.Any(c => !c.IsSystem)).ShouldAllBe(l => l.LastContactAt != null);

        // Phụ huynh L-0001: 2 con, có lịch sử tư vấn theo thứ tự, ngày tạo giữ theo mock (không phải lúc nạp)
        var parent = leads.Single(l => l.Id == new LeadId(SeedData.IdOf("L-0001")));
        parent.StudentLinks.Count.ShouldBe(2);
        parent.Consultations.Count(c => c.Result == ConsultResult.Enrolled).ShouldBeGreaterThanOrEqualTo(1);
        var created = await db.Leads.IgnoreQueryFilters().Where(l => l.Id == parent.Id)
            .Select(l => EF.Property<DateTimeOffset>(l, "CreatedAt")).SingleAsync(Ct);
        created.ShouldBe(mock.Single(m => m.GetProperty("id").GetString() == "L-0001").GetProperty("createdAt").GetDateTimeOffset(),
            TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Quy_tac_chia_lead_va_nhat_ky()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var ids = Items("rules").Select(r => new LeadRuleId(Id(r))).ToList();
        var rules = await db.LeadRules.AsNoTracking().Where(r => ids.Contains(r.Id)).OrderBy(r => r.Priority).ToListAsync(Ct);

        rules.Select(r => r.Id).ShouldBe(ids);
        var cg = rules[1];
        (cg.Method, cg.SlaMinutes, cg.ReassignOnBreach, cg.SaleIds.Count).ShouldBe((RouteMethod.RoundRobin, 15, true, 3)); // con trỏ đổi khi test khác chia lead
        rules.Last().Active.ShouldBeFalse();
        rules[0].Interests.ShouldBe(["xkld"]);

        (await db.LeadAssignmentLogs.CountAsync(l => l.RuleId != null, Ct)).ShouldBeGreaterThanOrEqualTo(Items("assignmentLogs").Count);
    }

    [Fact]
    public async Task Bai_test_va_luot_lam()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var tests = await db.PlacementTests.AsNoTracking().ToDictionaryAsync(t => t.Id.Value, Ct);
        var pt1 = tests[SeedData.IdOf("PT-01")];
        (pt1.Status, pt1.DurationMinutes, pt1.Sections.Count, pt1.Bands[0].MinPct).ShouldBe((PlacementTestStatus.Active, 25, 5, 0m));
        // Câu cố định theo mock (ngân hàng mẫu có thể ít hơn số câu yêu cầu của phần)
        var pt1Mock = Items("placementTests").Single(t => t.GetProperty("id").GetString() == "PT-01");
        pt1.QuestionIds.ShouldBe(pt1Mock.GetProperty("questionIds").EnumerateArray().Select(q => SeedData.IdOf(q.GetString()!)).ToList());
        tests[SeedData.IdOf("PT-03")].Status.ShouldBe(PlacementTestStatus.Draft);

        var mock = Items("attempts");
        var ids = mock.Select(a => new PlacementAttemptId(Id(a))).ToList();
        var attempts = await db.PlacementAttempts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(Ct);
        attempts.Count.ShouldBe(mock.Count);
        foreach (var m in mock)
            attempts.Single(a => a.Id == new PlacementAttemptId(Id(m))).Status
                .ShouldBe(SnakeCase<AttemptStatus>(m.GetProperty("status").GetString()!));
        attempts.ShouldAllBe(a => a.Token.Length >= PlacementAttemptErrors.MinTokenLength);

        // jsonb: đáp án + kết quả theo kỹ năng đọc lại đúng
        var done = attempts.Single(a => a.Id == new PlacementAttemptId(SeedData.IdOf("AT-1")));
        (done.Logged, done.IsGraded, done.Total).ShouldBe((true, true, pt1.QuestionIds.Count));
        done.BySkill.Values.Sum(s => s.Total).ShouldBe(done.Total!.Value);
        done.BySkill.Keys.ShouldContain(JapaneseSkill.Vocab);
        done.Answers.Keys.ShouldAllBe(q => pt1.QuestionIds.Contains(q));
        done.SuggestedLevel.ShouldNotBeNull();
    }

    [Fact]
    public async Task Lich_hen_chien_dich_mau_tin()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();

        var mockAppts = Items("appointments");
        var apptIds = mockAppts.Select(a => new AppointmentId(Id(a))).ToList();
        var appts = await db.Appointments.AsNoTracking().Where(a => apptIds.Contains(a.Id)).ToListAsync(Ct);
        appts.Count.ShouldBe(mockAppts.Count);
        appts.Count(a => a.Status == AppointmentStatus.Done).ShouldBe(mockAppts.Count(a => a.GetProperty("status").GetString() == "done"));
        appts.Where(a => a.Status == AppointmentStatus.Done).ShouldAllBe(a => a.Outcome != null);

        var campaigns = await db.Campaigns.AsNoTracking().ToDictionaryAsync(c => c.Code, Ct);
        var fb = campaigns["FB-N5-T11"];
        (fb.Status, fb.Utm, fb.Budget, fb.BranchIds.Count, fb.CourseIds.Count).ShouldBe((CampaignStatus.Running, "fb_n5_t11",
            Money.FromTrusted(6_000_000), 3, 1));
        campaigns["ZL-REF"].Status.ShouldBe(CampaignStatus.Paused);
        campaigns["EV-DUHOC-10"].Status.ShouldBe(CampaignStatus.Ended);
        campaigns["FB-TET27"].Status.ShouldBe(CampaignStatus.Planned);

        (await db.MessageTemplates.CountAsync(Ct)).ShouldBeGreaterThanOrEqualTo(Items("templates").Count);
    }

    private static T SnakeCase<T>(string value) where T : struct, Enum =>
        JPCenter.Infrastructure.Common.Persistence.Conversions.SnakeCaseEnumConverter<T>.Parse(value);
}
