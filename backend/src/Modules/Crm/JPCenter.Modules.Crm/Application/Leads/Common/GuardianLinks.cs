using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Crm.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Crm.Application.Leads.Common;

/// <summary>
/// Quy tắc liên kết lead ↔ HV xuyên nhiều lead (CRM-08): mỗi HV tối đa 1 người liên hệ chính (E2 — tắt ở lead khác),
/// cảnh báo HV nhỏ tuổi chưa có người quyết định (E3) / không ai nhận hóa đơn (E4), phát sự kiện cho cổng PH.
/// </summary>
public sealed class GuardianLinks(ICrmDbContext db, IEnrollmentLookup students, IIntegrationEventPublisher events, IClock clock)
{
    public const int AdultAge = 18;

    /// <summary>Tắt cờ "liên hệ chính" của HV ở các lead khác (gọi trước khi lưu). Trả cảnh báo nếu có đổi.</summary>
    public async Task<LeadWarning?> MakePrimaryAsync(Lead lead, Guid studentId, CancellationToken ct)
    {
        var others = await db.Leads.IgnoreQueryFilters()
            .Where(l => l.Id != lead.Id && l.StudentLinks.Any(k => k.StudentId == studentId && k.IsPrimary)).ToListAsync(ct);
        foreach (var o in others) o.ClearPrimary(studentId);
        return others.Count == 0 ? null
            : new LeadWarning("crm.lead.primary_changed", $"Đã đổi người liên hệ chính (trước: {string.Join(", ", others.Select(o => o.FullName))})", null);
    }

    /// <summary>Cảnh báo SAU KHI LƯU: HV nhỏ tuổi không còn người quyết định (E3), không ai nhận hóa đơn (E4)</summary>
    public async Task<IReadOnlyList<LeadWarning>> CheckStudentAsync(Guid studentId, CancellationToken ct)
    {
        var flags = await db.Leads.AsNoTracking().IgnoreQueryFilters()
            .SelectMany(l => l.StudentLinks.Where(k => k.StudentId == studentId).Select(k => new { k.IsPrimary, k.Billing }))
            .ToListAsync(ct);
        var student = (await students.SummariesAsync([studentId], ct)).FirstOrDefault();
        if (student is null) return [];
        var warnings = new List<LeadWarning>();
        if (IsMinor(student.Dob) && !flags.Any(f => f.IsPrimary))
            warnings.Add(new("crm.lead.no_decider", $"{student.FullName} chưa có người quyết định (liên hệ chính)", null));
        if (!flags.Any(f => f.Billing))
            warnings.Add(new("crm.lead.no_billing", $"Không còn ai nhận hóa đơn của {student.FullName} — hóa đơn gửi tới chính học viên", null));
        return warnings;
    }

    public bool IsMinor(DateOnly? dob)
    {
        if (dob is not { } d) return false;
        var today = clock.Today;
        var age = today.Year - d.Year - (today < d.AddYears(today.Year - d.Year) ? 1 : 0);
        return age < AdultAge;
    }

    public void Linked(Lead lead, Guid studentId) => events.Publish(new Ev.LeadStudentLinked(lead.Id.Value, studentId));

    public void Unlinked(Lead lead, Guid studentId) => events.Publish(new Ev.LeadStudentUnlinked(lead.Id.Value, studentId));
}
