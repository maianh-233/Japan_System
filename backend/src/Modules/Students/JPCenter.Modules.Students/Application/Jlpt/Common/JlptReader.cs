using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.Common;

/// <summary>HV-16 — đọc kỳ thi / lượt đăng ký (phạm vi theo chi nhánh của HV lúc đăng ký), dựng DTO (tên HV, tên nhân viên nhận / phát chứng chỉ)</summary>
public sealed class JlptReader(IStudentsDbContext db, StudentAccess access, EmployeeNames names, IClock clock)
{
    public JlptSessionDto ToDto(JlptSession s) => new(s.Id.Value, s.Label, s.ExamDate, s.RegistrationClose, s.ResultDate,
        s.Fees.OrderByDescending(f => f.Key).ToDictionary(f => f.Key.ToString(), f => f.Value.Amount), s.Venues, s.IsClosed(clock.Today));

    /// <summary>Lượt đăng ký trong phạm vi quyền <paramref name="action"/></summary>
    public IQueryable<JlptRegistration> Scoped(PermissionAction action)
    {
        var query = db.JlptRegistrations.AsQueryable();
        if (access.Branches(StudentAccess.Jlpt, action) is { } allowed)
        {
            var branches = allowed.Select(b => new BranchId(b)).ToList();
            query = query.Where(r => branches.Contains(r.BranchId));
        }
        return query;
    }

    public async Task<IReadOnlyList<JlptRegistrationDto>> ToDtosAsync(IQueryable<JlptRegistration> query, CancellationToken ct)
    {
        var rows = await (from r in query.AsNoTracking()
                          join s in db.Students.AsNoTracking().IgnoreQueryFilters() on r.StudentId equals s.Id
                          select new { R = r, s.Code, s.FullName, Created = EF.Property<DateTimeOffset>(r, "CreatedAt") }).ToListAsync(ct);
        var staff = await names.OfAsync(rows.Where(x => x.R.Certificate is not null)
            .SelectMany(x => new[] { x.R.Certificate!.ReceivedBy, x.R.Certificate.HandedBy ?? Guid.Empty }), ct);
        return rows.OrderBy(x => x.FullName).Select(x =>
        {
            var r = x.R;
            var judged = r.Scores.Count > 0 ? JlptScoring.Judge(r.Level, r.Scores) : null;
            var c = r.Certificate;
            return new JlptRegistrationDto(r.Id.Value, r.StudentId.Value, x.Code, x.FullName, r.BranchId.Value, r.SessionId.Value, JlptLevelText.Of(r.Level), r.Venue,
                r.Status, new JlptDocsDto(r.DocPhoto, r.DocIdCard, r.DocForm), r.Fee.Amount, r.PaidAt, r.AdmitNo, r.Scores.Count > 0 ? r.Scores : null,
                judged?.Total, r.Passed, judged?.FailSections ?? [],
                c is null ? null : new JlptCertificateDto(c.No, c.ReceivedAt, staff.GetValueOrDefault(c.ReceivedBy) ?? "—", c.HandedAt,
                    c.HandedBy is { } h ? staff.GetValueOrDefault(h) ?? "—" : null, c.HandedTo),
                r.Note, x.Created);
        }).ToList();
    }

    public async Task<JlptRegistrationDto> ReloadAsync(JlptRegistrationId id, CancellationToken ct) =>
        (await ToDtosAsync(db.JlptRegistrations.Where(r => r.Id == id), ct))[0];
}
