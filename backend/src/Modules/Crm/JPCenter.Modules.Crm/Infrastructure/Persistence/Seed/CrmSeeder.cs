using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp lead + liên kết lead ↔ học viên mẫu từ <c>crm.seed.json</c> (xuất từ mock frontend) — chỉ khi bảng lead còn trống.
/// Lead "Đã chốt" tạo ở trạng thái Mới rồi chốt sau khi liên kết học viên (domain chỉ cho chốt qua ghi danh).
/// </summary>
internal sealed class CrmSeeder(CrmDbContext db, ILogger<CrmSeeder> logger) : IDataSeeder
{
    public int Order => 45;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Leads.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<CrmSeedFile>(typeof(CrmSeeder).Assembly, "crm.seed.json");

        var leads = new Dictionary<string, Lead>();
        foreach (var l in data.Leads)
        {
            var status = SnakeCaseEnumConverter<LeadStatus>.Parse(l.Status);
            var info = new LeadInfo(l.FullName, l.Phone, l.Email, SnakeCaseEnumConverter<LeadSource>.Parse(l.Source),
                new BranchId(SeedData.IdOf(l.BranchId)), l.Interest, l.Note, null, null);
            // "Đã chốt" chỉ đạt qua ghi danh → tạo Mới rồi chốt sau khi liên kết học viên
            var created = Lead.Create(l.Code, info, status == LeadStatus.Won ? LeadStatus.New : status,
                status == LeadStatus.Lost ? l.LostReason ?? "Không rõ" : null);
            if (created.IsFailure)
            {
                logger.LogWarning("Bỏ qua lead mẫu {Code}: {Error}", l.Code, created.Error.Message);
                continue;
            }
            var lead = created.Value;
            lead.AssignTo(l.AssigneeId is null ? null : SeedData.IdOf(l.AssigneeId));
            db.AddWithId(lead, SeedData.IdOf(l.Id));
            leads[l.Id] = lead;
        }

        foreach (var k in data.Links)
        {
            if (!leads.TryGetValue(k.LeadId, out var lead)) continue;
            lead.LinkStudent(SeedData.IdOf(k.StudentId), k.StudentId, SnakeCaseEnumConverter<Relationship>.Parse(k.Relationship),
                k.Primary ?? false, k.Billing ?? false, k.Notify ?? false, k.CreatedAt);
        }

        foreach (var l in data.Leads.Where(x => leads.ContainsKey(x.Id)))
        {
            var lead = leads[l.Id];
            if (l.Status == "won" && lead.StudentLinks.Count > 0)
                lead.MarkWon(lead.StudentLinks[0].StudentId, "Ghi danh (dữ liệu mẫu)", lead.StudentLinks[0].CreatedAt);
            if (l.Hidden) lead.Hide(l.HiddenAt ?? DateTimeOffset.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dữ liệu mẫu Crm: {Leads} lead, {Links} liên kết học viên", leads.Count, leads.Values.Sum(l => l.StudentLinks.Count));
    }

    private sealed record CrmSeedFile(List<SeedLead> Leads, List<SeedLink> Links);

    private sealed record SeedLead(string Id, string Code, string FullName, string Phone, string? Email, string Source, string Status, string BranchId,
        string? AssigneeId, string? Interest, string? Note, string? LostReason, bool Hidden, DateTimeOffset? HiddenAt);

    private sealed record SeedLink(string LeadId, string StudentId, string Relationship, DateTimeOffset CreatedAt, bool? Primary, bool? Billing,
        bool? Notify);
}
