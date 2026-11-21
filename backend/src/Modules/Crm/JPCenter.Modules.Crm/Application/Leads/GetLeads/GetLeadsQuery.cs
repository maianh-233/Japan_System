using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Paging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Leads.GetLeads;

/// <summary>Bộ lọc danh sách / Kanban lead (CRM-01 bước 2)</summary>
/// <param name="Q">Tìm theo tên / SĐT / mã / email</param>
/// <param name="Hidden">true = chỉ lead đã ẩn · false/null = chỉ lead chưa ẩn (E4)</param>
public sealed record LeadFilter(LeadStatus? Status, LeadSource? Source, Guid? BranchId, Guid? AssigneeId, bool Unassigned, Guid? CampaignId,
    bool SlaOverdue, bool FollowUpOverdue, bool? Hidden, string? Q);

/// <summary>CRM-01 — danh sách lead theo phạm vi (own = lead mình phụ trách), mới tạo trước, phân trang.</summary>
public sealed record GetLeadsQuery(LeadFilter Filter, PageRequest Page) : IQuery<PagedResult<LeadDto>>;

public sealed class GetLeadsHandler(ICrmDbContext db, CrmAccess access, LeadReader reader, IClock clock) : IQueryHandler<GetLeadsQuery, PagedResult<LeadDto>>
{
    private static readonly string[] Sorts = ["createdAt", "code", "fullName", "lastContactAt", "nextFollowUpAt"];

    public async Task<Result<PagedResult<LeadDto>>> Handle(GetLeadsQuery query, CancellationToken ct)
    {
        var q = Apply(access.Scope(db.Leads.AsNoTracking(), PermissionAction.View), query.Filter, clock.UtcNow);
        if (query.Filter.Status is { } status) q = q.Where(l => l.Status == status);
        q = Sort(q, query.Page.Sort);

        var page = query.Page;
        if (!query.Filter.SlaOverdue)
        {
            var total = await q.CountAsync(ct);
            var items = await q.Skip((page.SafePage - 1) * page.SafePageSize).Take(page.SafePageSize).ToListAsync(ct);
            return new PagedResult<LeadDto>(await reader.ToDtosAsync(items, ct), total, page.SafePage, page.SafePageSize);
        }

        // Quá SLA phụ thuộc quy tắc khớp từng lead → lọc trong bộ nhớ trên tập ứng viên (lead Mới chưa liên hệ, không ẩn)
        var candidates = await q.Where(l => l.Status == LeadStatus.New && l.LastContactAt == null && !l.Hidden).ToListAsync(ct);
        var overdue = (await reader.ToDtosAsync(candidates, ct)).Where(d => d.SlaOverdue).ToList();
        return new PagedResult<LeadDto>(overdue.Skip((page.SafePage - 1) * page.SafePageSize).Take(page.SafePageSize).ToList(), overdue.Count,
            page.SafePage, page.SafePageSize);
    }

    /// <summary>Bộ lọc chung cho danh sách + Kanban (không gồm giai đoạn)</summary>
    public static IQueryable<Lead> Apply(IQueryable<Lead> q, LeadFilter f, DateTimeOffset now)
    {
        q = f.Hidden == true ? q.Where(l => l.Hidden) : q.Where(l => !l.Hidden);
        if (f.Source is { } source) q = q.Where(l => l.Source == source);
        if (f.BranchId is { } branch) q = q.Where(l => l.BranchId == new BranchId(branch));
        if (f.AssigneeId is { } sale) q = q.Where(l => l.AssigneeId == sale);
        if (f.Unassigned) q = q.Where(l => l.AssigneeId == null);
        if (f.CampaignId is { } campaign) q = q.Where(l => l.CampaignId == new CampaignId(campaign));
        if (f.FollowUpOverdue)
            q = q.Where(l => l.NextFollowUpAt < now && l.Status != LeadStatus.Won && l.Status != LeadStatus.Lost);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var text = f.Q.Trim();
            var pattern = $"%{text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
            var phone = PhoneNumber.Normalize(text);
            q = q.Where(l => EF.Functions.ILike(l.FullName, pattern) || EF.Functions.ILike(l.Code, pattern)
                || (l.Email != null && EF.Functions.ILike(l.Email, pattern)) || (phone != null && l.Phone.Contains(phone)));
        }
        return q;
    }

    private static IQueryable<Lead> Sort(IQueryable<Lead> q, string? sort)
    {
        var desc = sort?.StartsWith('-') ?? true;
        var key = sort?.TrimStart('-');
        if (key is null || !Sorts.Contains(key)) key = "createdAt";
        return (key, desc) switch
        {
            ("code", false) => q.OrderBy(l => l.Code),
            ("code", true) => q.OrderByDescending(l => l.Code),
            ("fullName", false) => q.OrderBy(l => l.FullName),
            ("fullName", true) => q.OrderByDescending(l => l.FullName),
            ("lastContactAt", false) => q.OrderBy(l => l.LastContactAt),
            ("lastContactAt", true) => q.OrderByDescending(l => l.LastContactAt),
            ("nextFollowUpAt", false) => q.OrderBy(l => l.NextFollowUpAt),
            ("nextFollowUpAt", true) => q.OrderByDescending(l => l.NextFollowUpAt),
            (_, false) => q.OrderBy(l => EF.Property<DateTimeOffset>(l, "CreatedAt")).ThenBy(l => l.Code),
            _ => q.OrderByDescending(l => EF.Property<DateTimeOffset>(l, "CreatedAt")).ThenByDescending(l => l.Code),
        };
    }
}
