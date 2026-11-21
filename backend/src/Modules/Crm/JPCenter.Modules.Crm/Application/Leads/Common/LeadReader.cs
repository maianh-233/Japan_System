using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Leads.Common;

/// <summary>Dựng DTO lead: tên Sale (Hr), SLA theo quy tắc khớp / Cài đặt chung, ngày tạo + phiên bản (cột chung), HV liên kết (Students).</summary>
public sealed class LeadReader(ICrmDbContext db, PeopleNames people, ICenterSettings settings, IEnrollmentLookup students, IClock clock)
{
    /// <summary>SLA áp cho từng lead: quy tắc đang bật khớp đầu tiên, không có → mặc định</summary>
    public async Task<Func<Lead, (int Sla, LeadRule? Rule)>> SlaOfAsync(CancellationToken ct)
    {
        var rules = await db.LeadRules.AsNoTracking().Where(r => r.Active).ToListAsync(ct);
        var fallback = (await settings.GetAsync(ct)).CrmDefaultSlaMinutes;
        return lead => LeadRouter.MatchRule(rules, lead.Source, lead.BranchId, lead.Interest) is { } r ? (r.SlaMinutes, r) : (fallback, null);
    }

    /// <summary>Ngày tạo (cột chung created_at) + phiên bản xmin</summary>
    public async Task<Dictionary<LeadId, (DateTimeOffset Created, uint Version)>> MetaAsync(IReadOnlyCollection<LeadId> ids, CancellationToken ct) =>
        (await db.Leads.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, Created = EF.Property<DateTimeOffset>(l, "CreatedAt"), Version = EF.Property<uint>(l, "xmin") })
            .ToListAsync(ct))
        .ToDictionary(x => x.Id, x => (x.Created, x.Version));

    public async Task<IReadOnlyList<LeadDto>> ToDtosAsync(IReadOnlyList<Lead> leads, CancellationToken ct)
    {
        if (leads.Count == 0) return [];
        var meta = await MetaAsync(leads.Select(l => l.Id).ToList(), ct);
        var names = await people.OfAsync(leads.Select(l => l.AssigneeId), ct);
        var slaOf = await SlaOfAsync(ct);
        var now = clock.UtcNow;
        return leads.Select(l =>
        {
            var (created, version) = meta.GetValueOrDefault(l.Id);
            var (sla, _) = slaOf(l);
            return new LeadDto(l.Id.Value, l.Code, l.FullName, l.Phone, l.Email, l.Source, l.Status, l.BranchId.Value, l.AssigneeId,
                l.AssigneeId is { } a ? names.GetValueOrDefault(a) : null, l.Interest, l.Note, l.LostReason, l.NextFollowUpAt, l.LastContactAt,
                created, l.Hidden, l.HiddenAt, l.NotifyChannels, l.CampaignId?.Value, l.SlaEscalatedAt, l.SlaReassignedAt,
                l.IsSlaOverdue(created, sla, now), sla, l.IsFollowUpOverdue(now), l.StudentLinks.Count, version);
        }).ToList();
    }

    public async Task<LeadDto> ToDtoAsync(Lead lead, CancellationToken ct) => (await ToDtosAsync([lead], ct))[0];

    public async Task<LeadDetailDto> DetailAsync(Lead lead, CancellationToken ct)
    {
        var dto = await ToDtoAsync(lead, ct);
        var consultations = lead.Consultations.OrderByDescending(c => c.CreatedAt).Select(c => ToDto(lead.Id, c)).ToList();
        return new LeadDetailDto(dto, consultations, await LinksAsync(lead, ct));
    }

    public static ConsultationDto ToDto(LeadId leadId, Consultation c) =>
        new(c.Id.Value, leadId.Value, c.Channel, c.Result, c.Content, c.StaffId, c.StaffName, c.CreatedAt, c.NextFollowUpAt, c.IsSystem);

    /// <summary>HV liên kết + người liên hệ khác của cùng HV (kể cả lead đã ẩn)</summary>
    public async Task<IReadOnlyList<LeadLinkDto>> LinksAsync(Lead lead, CancellationToken ct)
    {
        if (lead.StudentLinks.Count == 0) return [];
        var ids = lead.StudentLinks.Select(k => k.StudentId).ToList();
        var summaries = (await students.SummariesAsync(ids, ct)).ToDictionary(s => s.Id);
        var others = await db.Leads.AsNoTracking().IgnoreQueryFilters()
            .Where(l => l.Id != lead.Id && l.StudentLinks.Any(k => ids.Contains(k.StudentId)))
            .Select(l => new { l.Id, l.Code, l.FullName, Links = l.StudentLinks.Where(k => ids.Contains(k.StudentId)).ToList() })
            .ToListAsync(ct);
        return lead.StudentLinks.OrderBy(k => k.CreatedAt).Select(k =>
        {
            var s = summaries.GetValueOrDefault(k.StudentId);
            var contacts = others.SelectMany(o => o.Links.Where(x => x.StudentId == k.StudentId)
                .Select(x => new OtherContactDto(o.Id.Value, o.Code, o.FullName, x.Relationship, x.IsPrimary))).ToList();
            return new LeadLinkDto(lead.Id.Value, k.StudentId, s?.Code ?? "—", s?.FullName ?? "(không tìm thấy)", s?.Dob, k.Relationship,
                k.CreatedAt, k.IsPrimary, k.Billing, k.Notify, contacts);
        }).ToList();
    }

    /// <summary>CRM-02.E3 — lead khác cùng SĐT (kể cả đã ẩn)</summary>
    public async Task<IReadOnlyList<LeadWarning>> DuplicatesAsync(string phone, LeadId? except, CancellationToken ct)
    {
        var normalized = PhoneNumber.Normalize(phone);
        if (normalized is null) return [];
        var dupes = await db.Leads.AsNoTracking().IgnoreQueryFilters()
            .Where(l => l.Phone == normalized && (except == null || l.Id != except))
            .OrderBy(l => l.Code).Select(l => new { l.Id, l.Code, l.FullName }).Take(5).ToListAsync(ct);
        return dupes.Select(d => new LeadWarning(LeadErrors.DuplicatePhone.Code, $"Trùng với lead {d.Code} — {d.FullName}", d.Id.Value)).ToList();
    }
}
