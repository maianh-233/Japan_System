using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp lead + liên kết lead ↔ học viên mẫu từ <c>crm.seed.json</c> (xuất từ mock frontend) — chỉ khi bảng lead còn trống.
/// Liên kết làm lead chuyển "Đã chốt" (domain) → đặt lại trạng thái theo mock sau khi liên kết. Lịch sử tư vấn mẫu nạp ở giai đoạn 16.
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
            var created = Lead.Create(l.Code, l.FullName, l.Phone, l.Email, SnakeCaseEnumConverter<LeadSource>.Parse(l.Source),
                new BranchId(SeedData.IdOf(l.BranchId)), l.Interest, l.Note, null);
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
            lead.LinkStudent(SeedData.IdOf(k.StudentId), SnakeCaseEnumConverter<Relationship>.Parse(k.Relationship), k.Primary ?? false,
                k.Billing ?? false, k.Notify ?? false, k.CreatedAt);
        }

        foreach (var l in data.Leads.Where(x => leads.ContainsKey(x.Id)))
        {
            var lead = leads[l.Id];
            lead.ChangeStatus(SnakeCaseEnumConverter<LeadStatus>.Parse(l.Status), l.LostReason ?? (l.Status == "lost" ? "Không rõ" : null));
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
