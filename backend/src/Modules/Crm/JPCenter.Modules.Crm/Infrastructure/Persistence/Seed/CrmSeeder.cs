using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp dữ liệu mẫu Tuyển sinh từ <c>crm.seed.json</c> (xuất từ mock frontend). Mỗi bảng chỉ nạp khi CÒN TRỐNG, không xóa gì —
/// DB đã có lead từ giai đoạn 15 thì chỉ bổ sung lịch sử tư vấn, chiến dịch, ngày tạo cho lead mẫu.
/// Mọi thay đổi đi qua method domain; lead "Đã chốt" tạo Mới rồi chốt sau khi liên kết học viên.
/// </summary>
internal sealed class CrmSeeder(CrmDbContext db, IClock clock, ILogger<CrmSeeder> logger) : IDataSeeder
{
    public int Order => 45;

    public async Task SeedAsync(CancellationToken ct)
    {
        var data = SeedData.Read<CrmSeedFile>(typeof(CrmSeeder).Assembly, "crm.seed.json");
        var counts = new List<string>();

        if (!await db.Campaigns.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedCampaigns(data)} chiến dịch");
        if (!await db.Leads.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedLeads(data)} lead");
        else if (!await db.Leads.IgnoreQueryFilters().AnyAsync(l => l.Consultations.Any(), ct))
            counts.Add($"bổ sung {await BackfillLeadsAsync(data, ct)} lead");
        if (!await db.LeadRules.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedRules(data)} quy tắc chia lead");
        if (!await db.MessageTemplates.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedTemplates(data)} mẫu tin");
        if (!await db.PlacementTests.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedPlacementTests(data)} bài test");

        // Bảng trỏ tới lead / quy tắc / bài test: chỉ nạp dòng có lead (đã có trong DB hoặc vừa thêm)
        var leadIds = (await db.Leads.IgnoreQueryFilters().Select(l => l.Id).ToListAsync(ct))
            .Concat(db.ChangeTracker.Entries<Lead>().Select(e => e.Entity.Id)).ToHashSet();
        if (!await db.LeadAssignmentLogs.AnyAsync(ct)) counts.Add($"{SeedAssignmentLogs(data, leadIds)} nhật ký chia lead");
        if (!await db.PlacementAttempts.AnyAsync(ct)) counts.Add($"{SeedAttempts(data, leadIds)} lượt làm test");
        if (!await db.Appointments.AnyAsync(ct)) counts.Add($"{SeedAppointments(data, leadIds)} lịch hẹn");

        if (counts.Count == 0) return;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dữ liệu mẫu Crm: {Counts}", string.Join(", ", counts));
    }

    // ------------------------------------------------------------------ Chiến dịch

    private int SeedCampaigns(CrmSeedFile data)
    {
        foreach (var c in data.Campaigns)
        {
            var created = Campaign.Create(new CampaignDetails(c.Code, c.Name, Enum<LeadSource>(c.Channel), c.BranchIds.Select(Branch).ToList(),
                DateOnly.Parse(c.StartDate), DateOnly.Parse(c.EndDate), Money.FromTrusted(c.Budget), Money.FromTrusted(c.Spent), c.TargetLeads,
                Person(c.OwnerId), c.Utm, c.CourseIds.Select(SeedData.IdOf).ToList(), c.Note));
            if (Skip(created, c.Code)) continue;
            var campaign = created.Value;
            _ = c.Status switch
            {
                "running" => campaign.Start(),
                "paused" => Chain(campaign.Start(), campaign.Pause),
                "ended" => campaign.End(),
                _ => Result.Success(),
            };
            db.AddWithId(campaign, SeedData.IdOf(c.Id));
            db.SetCreatedAt(campaign, c.CreatedAt);
        }
        return data.Campaigns.Count;
    }

    // ------------------------------------------------------------------ Lead

    private int SeedLeads(CrmSeedFile data)
    {
        var added = 0;
        foreach (var l in data.Leads)
        {
            var info = new LeadInfo(l.FullName, l.Phone, l.Email, Enum<LeadSource>(l.Source), Branch(l.BranchId), l.Interest, l.Note,
                CampaignOf(l.CampaignId), l.NextFollowUpAt);
            var created = Lead.Create(l.Code, info);
            if (Skip(created, l.Code)) continue;
            var lead = created.Value;
            lead.AssignTo(l.AssigneeId is null ? null : SeedData.IdOf(l.AssigneeId));
            lead.SetNotifyChannels(l.NotifyChannels.Select(Enum<ContactChannel>));
            db.AddWithId(lead, SeedData.IdOf(l.Id));
            db.SetCreatedAt(lead, l.CreatedAt);

            ReplayConsultations(lead, data.Consultations.Where(c => c.LeadId == l.Id));
            foreach (var k in data.Links.Where(k => k.LeadId == l.Id).OrderBy(k => k.CreatedAt))
                lead.LinkStudent(SeedData.IdOf(k.StudentId), k.StudentName, Enum<Relationship>(k.Relationship), k.Primary ?? false,
                    k.Billing ?? false, k.Notify ?? false, k.CreatedAt);

            var at = lead.Consultations.Select(c => c.CreatedAt).DefaultIfEmpty(l.CreatedAt).Max();
            if (l.Status == "won" && lead.StudentLinks.Count > 0)
            {
                var first = lead.StudentLinks[0];
                var name = data.Links.First(k => k.LeadId == l.Id && SeedData.IdOf(k.StudentId) == first.StudentId).StudentName;
                lead.MarkWon(first.StudentId, $"Ghi danh học viên {name}", first.CreatedAt);
            }
            else if (l.Status != "won") lead.ChangeStatus(Enum<LeadStatus>(l.Status), l.LostReason ?? "Không rõ", at);

            if (l.NextFollowUpAt is { } follow) lead.ScheduleFollowUp(follow);
            if (l.SlaEscalatedAt is { } esc) lead.MarkSlaEscalated(esc);
            if (l.SlaReassignedAt is { } re && lead.AssigneeId is { } sale) lead.ReassignForSla(sale, re);
            if (l.Hidden) lead.Hide(l.HiddenAt ?? l.CreatedAt);
            added++;
        }
        return added;
    }

    /// <summary>DB đã có lead (giai đoạn 15): thêm lịch sử tư vấn, chiến dịch, hẹn gọi lại, ngày tạo theo mock — không đổi giai đoạn đã lưu.</summary>
    private async Task<int> BackfillLeadsAsync(CrmSeedFile data, CancellationToken ct)
    {
        var leads = await db.Leads.IgnoreQueryFilters().ToDictionaryAsync(l => l.Id, ct);
        var touched = 0;
        foreach (var l in data.Leads)
        {
            if (!leads.TryGetValue(new LeadId(SeedData.IdOf(l.Id)), out var lead)) continue;
            var status = lead.Status;
            lead.Update(new LeadInfo(lead.FullName, lead.Phone, lead.Email, lead.Source, lead.BranchId, lead.Interest, lead.Note,
                CampaignOf(l.CampaignId), l.NextFollowUpAt));
            lead.SetNotifyChannels(l.NotifyChannels.Select(Enum<ContactChannel>));
            ReplayConsultations(lead, data.Consultations.Where(c => c.LeadId == l.Id));
            if (lead.Status != status && status != LeadStatus.Won) lead.ChangeStatus(status, lead.LostReason ?? "Không rõ", clock.UtcNow);
            if (l.NextFollowUpAt is { } follow) lead.ScheduleFollowUp(follow);
            db.SetCreatedAt(lead, l.CreatedAt);
            touched++;
        }
        return touched;
    }

    private static void ReplayConsultations(Lead lead, IEnumerable<SeedConsultation> consultations)
    {
        foreach (var c in consultations.OrderBy(c => c.CreatedAt))
        {
            if (c.System == true || c.StaffId is null) lead.AddSystemNote(c.Content, c.CreatedAt, Enum<ConsultResult>(c.Result));
            else
                lead.AddConsultation(Enum<ConsultChannel>(c.Channel), Enum<ConsultResult>(c.Result), c.Content, SeedData.IdOf(c.StaffId),
                    c.StaffName, c.CreatedAt, c.NextFollowUpAt);
        }
    }

    // ------------------------------------------------------------------ Chia lead

    private int SeedRules(CrmSeedFile data)
    {
        var priority = 0;
        foreach (var r in data.Rules)
        {
            var created = LeadRule.Create(new LeadRuleSettings(r.Name, r.Sources.Select(Enum<LeadSource>).ToList(),
                r.BranchIds.Select(Branch).ToList(), r.Interests, Enum<RouteMethod>(r.Method), r.SaleIds.Select(SeedData.IdOf).ToList(),
                r.OnlyOnShift, r.SlaMinutes, r.EscalateToIds.Select(SeedData.IdOf).ToList(), r.ReassignOnBreach), ++priority);
            if (Skip(created, r.Name)) continue;
            var rule = created.Value;
            rule.MoveCursor(r.Cursor);
            if (!r.Active) rule.Disable();
            db.AddWithId(rule, SeedData.IdOf(r.Id));
        }
        return data.Rules.Count;
    }

    private int SeedAssignmentLogs(CrmSeedFile data, HashSet<LeadId> leadIds)
    {
        var added = 0;
        foreach (var x in data.AssignmentLogs.Where(x => leadIds.Contains(new LeadId(SeedData.IdOf(x.LeadId)))))
        {
            var log = LeadAssignmentLog.Create(new LeadId(SeedData.IdOf(x.LeadId)), x.SaleId is null ? null : SeedData.IdOf(x.SaleId),
                x.RuleId is null ? null : new LeadRuleId(SeedData.IdOf(x.RuleId)), x.Reason, x.At);
            db.Add(log);
            added++;
        }
        return added;
    }

    // ------------------------------------------------------------------ Test xếp lớp

    private int SeedPlacementTests(CrmSeedFile data)
    {
        foreach (var t in data.PlacementTests)
        {
            var created = PlacementTest.Create(t.Code, t.Name, t.Description,
                t.Sections.Select(s => new PlacementSection(System.Enum.Parse<JlptLevel>(s.Level), Enum<JapaneseSkill>(s.Skill), s.Count)).ToList(),
                t.DurationMin, t.Bands.Select(b => new PlacementBand(b.MinPct, b.Level, b.Course)).ToList(),
                t.QuestionIds.Select(SeedData.IdOf).ToList(), Person(t.AuthorId));
            if (Skip(created, t.Code)) continue;
            var test = created.Value;
            if (t.Status is "active" or "archived") test.Activate();
            if (t.Status == "archived") test.Archive();
            db.AddWithId(test, SeedData.IdOf(t.Id));
            db.SetCreatedAt(test, t.CreatedAt);
        }
        return data.PlacementTests.Count;
    }

    private int SeedAttempts(CrmSeedFile data, HashSet<LeadId> leadIds)
    {
        var tests = db.ChangeTracker.Entries<PlacementTest>().Select(e => e.Entity)
            .Concat(db.PlacementTests.IgnoreQueryFilters().AsEnumerable()).DistinctBy(t => t.Id).ToDictionary(t => t.Id);
        var added = 0;
        foreach (var a in data.Attempts)
        {
            var leadId = new LeadId(SeedData.IdOf(a.LeadId));
            if (!leadIds.Contains(leadId) || !tests.TryGetValue(new PlacementTestId(SeedData.IdOf(a.TestId)), out var test)) continue;
            // Token mock ngắn → nối thêm 32 ký tự cố định cho đủ 128 bit
            var token = $"{a.Token}-{SeedData.IdOf(a.Id):N}";
            var days = Math.Clamp((int)Math.Ceiling((a.ExpiresAt - a.SentAt).TotalDays), 1, PlacementAttemptErrors.MaxValidDays);
            var wasActive = test.Status == PlacementTestStatus.Active;
            if (!wasActive) test.Activate();
            var sent = PlacementAttempt.Send(test, leadId, token, Enum<ContactChannel>(a.Channel), Person(a.SentById), a.SentAt, days);
            if (!wasActive) test.Archive();
            if (Skip(sent, a.Id)) continue;
            var attempt = sent.Value;

            if (a.StartedAt is { } started) attempt.Start(started);
            if (a.Status == "done" && a.Total is { } total)
            {
                var score = new PlacementScore(a.Correct ?? 0, total, (a.BySkill ?? []).ToDictionary(
                    kv => Enum<JapaneseSkill>(kv.Key), kv => new SkillScore(kv.Value.Correct, kv.Value.Total)));
                var answers = (a.Answers ?? []).ToDictionary(kv => SeedData.IdOf(kv.Key), kv => kv.Value);
                attempt.Submit(answers, score, test.Suggest(score.Percent), a.SubmittedAt ?? a.SentAt);
            }
            if (a.Status == "expired") attempt.ExpireIfDue(a.ExpiresAt.AddSeconds(1), _ => (new PlacementScore(0, 0, []), null));
            if (a.Logged == true) attempt.MarkLogged();
            db.AddWithId(attempt, SeedData.IdOf(a.Id));
            added++;
        }
        return added;
    }

    // ------------------------------------------------------------------ Lịch hẹn, mẫu tin

    private int SeedAppointments(CrmSeedFile data, HashSet<LeadId> leadIds)
    {
        var added = 0;
        foreach (var x in data.Appointments)
        {
            var leadId = new LeadId(SeedData.IdOf(x.LeadId));
            if (!leadIds.Contains(leadId)) continue;
            var created = Appointment.Create(x.Code, leadId, Enum<AppointmentKind>(x.Kind), Branch(x.BranchId),
                x.RoomId is null ? null : SeedData.IdOf(x.RoomId), SeedData.IdOf(x.StaffId), x.Start, x.End, x.Note, x.CreatedAt);
            if (Skip(created, x.Code)) continue;
            var appt = created.Value;
            _ = x.Status switch
            {
                "done" => appt.MarkDone(x.Outcome ?? "Khách đã đến"),
                "no_show" => appt.MarkNoShow(x.End),
                "cancelled" => appt.Cancel(x.CancelReason ?? "Không rõ"),
                _ => Result.Success(),
            };
            if (x.RemindedAt is { } reminded) appt.MarkReminded(reminded);
            db.AddWithId(appt, SeedData.IdOf(x.Id));
            db.SetCreatedAt(appt, x.CreatedAt);
            added++;
        }
        return added;
    }

    private int SeedTemplates(CrmSeedFile data)
    {
        foreach (var t in data.Templates)
        {
            var created = MessageTemplate.Create(Enum<MessageScenario>(t.Scenario), t.Name, Enum<ContactChannel>(t.Channel), t.Body);
            if (Skip(created, t.Name)) continue;
            if (!t.Active) created.Value.SetActive(false);
            db.AddWithId(created.Value, SeedData.IdOf(t.Id));
        }
        return data.Templates.Count;
    }

    // ------------------------------------------------------------------ Tiện ích

    private bool Skip<T>(Result<T> result, string label)
    {
        if (result.IsSuccess) return false;
        logger.LogWarning("Bỏ qua dữ liệu mẫu Crm {Label}: {Error}", label, result.Error.Message);
        return true;
    }

    private static Result Chain(Result first, Func<Result> next) => first.IsFailure ? first : next();

    private static T Enum<T>(string value) where T : struct, System.Enum => SnakeCaseEnumConverter<T>.Parse(value);

    private static BranchId Branch(string mockId) => new(SeedData.IdOf(mockId));

    private static CampaignId? CampaignOf(string? mockId) => mockId is null ? null : new CampaignId(SeedData.IdOf(mockId));

    /// <summary>Nhân viên theo Id mock; không có (tên không khớp nhân viên nào) → Guid rỗng</summary>
    private static Guid Person(string? mockId) => mockId is null ? Guid.Empty : SeedData.IdOf(mockId);

    // ------------------------------------------------------------------ Định dạng file

    private sealed record CrmSeedFile(List<SeedLead> Leads, List<SeedConsultation> Consultations, List<SeedLink> Links, List<SeedRule> Rules,
        List<SeedAssignmentLog> AssignmentLogs, List<SeedPlacementTest> PlacementTests, List<SeedAttempt> Attempts,
        List<SeedAppointment> Appointments, List<SeedCampaign> Campaigns, List<SeedTemplate> Templates);

    private sealed record SeedLead(string Id, string Code, string FullName, string Phone, string? Email, string Source, string Status, string BranchId,
        string? AssigneeId, string? Interest, string? Note, string? LostReason, bool Hidden, DateTimeOffset? HiddenAt, DateTimeOffset CreatedAt,
        DateTimeOffset? NextFollowUpAt, List<string> NotifyChannels, string? CampaignId, DateTimeOffset? SlaEscalatedAt,
        DateTimeOffset? SlaReassignedAt);

    private sealed record SeedConsultation(string LeadId, string Channel, string Result, string Content, string StaffName, string? StaffId,
        DateTimeOffset CreatedAt, DateTimeOffset? NextFollowUpAt, bool? System);

    private sealed record SeedLink(string LeadId, string StudentId, string StudentName, string Relationship, DateTimeOffset CreatedAt,
        bool? Primary, bool? Billing, bool? Notify);

    private sealed record SeedRule(string Id, string Name, bool Active, List<string> Sources, List<string> BranchIds, List<string> Interests,
        string Method, List<string> SaleIds, bool OnlyOnShift, int SlaMinutes, List<string> EscalateToIds, bool ReassignOnBreach, int Cursor);

    private sealed record SeedAssignmentLog(string LeadId, string? SaleId, string? RuleId, DateTimeOffset At, string Reason);

    private sealed record SeedSection(string Level, string Skill, int Count);

    private sealed record SeedBand(decimal MinPct, string Level, string Course);

    private sealed record SeedPlacementTest(string Id, string Code, string Name, string? Description, List<SeedSection> Sections,
        List<string> QuestionIds, int DurationMin, List<SeedBand> Bands, string Status, string? AuthorId, DateTimeOffset CreatedAt);

    private sealed record SeedSkillScore(int Correct, int Total);

    private sealed record SeedAttempt(string Id, string TestId, string LeadId, string Token, string Channel, string? SentById,
        DateTimeOffset SentAt, DateTimeOffset ExpiresAt, string Status, DateTimeOffset? StartedAt, DateTimeOffset? SubmittedAt,
        Dictionary<string, int>? Answers, int? Correct, int? Total, Dictionary<string, SeedSkillScore>? BySkill, bool? Logged);

    private sealed record SeedAppointment(string Id, string Code, string LeadId, string Kind, string BranchId, string? RoomId, string StaffId,
        DateTimeOffset Start, DateTimeOffset End, string Status, string? Note, string? Outcome, DateTimeOffset CreatedAt,
        DateTimeOffset? RemindedAt, string? CancelReason);

    private sealed record SeedCampaign(string Id, string Code, string Name, string Channel, List<string> BranchIds, string StartDate,
        string EndDate, decimal Budget, decimal Spent, int? TargetLeads, string Status, string? OwnerId, string? Utm, List<string> CourseIds,
        string? Note, DateTimeOffset CreatedAt);

    private sealed record SeedTemplate(string Id, string Scenario, string Name, string Channel, string Body, bool Active);
}
