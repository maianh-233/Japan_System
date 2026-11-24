using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.GetJlptStats;

/// <summary>
/// HV-16 bước 4 — tỉ lệ đỗ theo cấp độ / lớp (tính khi đọc, không lưu cột). Lớp = ghi danh gần nhất của HV bắt đầu trước ngày thi.
/// Tỉ lệ đỗ = đỗ ÷ số đã có kết quả (vắng không tính). Lượt đã hủy bỏ qua. <paramref name="SessionId"/> null = mọi kỳ.
/// </summary>
public sealed record GetJlptStatsQuery(Guid? SessionId) : IQuery<JlptStatsDto>;

public sealed class GetJlptStatsHandler(IStudentsDbContext db, JlptReader reader, IClassLookup classes) : IQueryHandler<GetJlptStatsQuery, JlptStatsDto>
{
    public async Task<Result<JlptStatsDto>> Handle(GetJlptStatsQuery q, CancellationToken ct)
    {
        var query = reader.Scoped(PermissionAction.View).AsNoTracking().Where(r => r.Status != JlptRegistrationStatus.Cancelled);
        if (q.SessionId is { } sid) query = query.Where(r => r.SessionId == new JlptSessionId(sid));
        var regs = await (from r in query
                          join s in db.JlptSessions.AsNoTracking() on r.SessionId equals s.Id
                          select new { r.StudentId, r.Level, r.Status, r.Passed, s.ExamDate }).ToListAsync(ct);

        var students = regs.Select(r => r.StudentId).Distinct().ToList();
        var enrollments = (await db.Enrollments.AsNoTracking().IgnoreQueryFilters().Where(e => students.Contains(e.StudentId))
            .Select(e => new { e.StudentId, e.ClassId, e.EnrolledAt }).ToListAsync(ct)).ToLookup(e => e.StudentId);
        var rows = regs.Select(r => new
        {
            r.Level, r.Status, r.Passed,
            ClassId = enrollments[r.StudentId].Where(e => e.EnrolledAt <= r.ExamDate).MaxBy(e => e.EnrolledAt)?.ClassId,
        }).ToList();
        var cls = await classes.FindManyAsync(rows.Where(r => r.ClassId is not null).Select(r => r.ClassId!.Value).Distinct().ToList(), ct);

        JlptPassRate Rate(string key, string label, IEnumerable<(JlptRegistrationStatus Status, bool? Passed)> items)
        {
            var list = items.ToList();
            var sat = list.Count(i => i.Status == JlptRegistrationStatus.Done);
            var passed = list.Count(i => i.Status == JlptRegistrationStatus.Done && i.Passed == true);
            return new JlptPassRate(key, label, list.Count, sat, list.Count(i => i.Status == JlptRegistrationStatus.Absent), passed,
                sat == 0 ? null : (int)Math.Round(passed * 100m / sat));
        }

        var byLevel = rows.GroupBy(r => r.Level).OrderByDescending(g => g.Key)
            .Select(g => Rate(g.Key.ToString(), g.Key.ToString(), g.Select(r => (r.Status, r.Passed)))).ToList();
        var byClass = rows.GroupBy(r => r.ClassId)
            .Select(g => g.Key is { } id && cls.TryGetValue(id, out var c)
                ? Rate(id.ToString(), c.Code, g.Select(r => (r.Status, r.Passed)))
                : Rate("none", "Không theo lớp", g.Select(r => (r.Status, r.Passed))))
            .OrderBy(r => r.Key == "none").ThenBy(r => r.Label).ToList();
        return new JlptStatsDto(byLevel, byClass);
    }
}
