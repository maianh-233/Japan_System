using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.CareCases.Common;

/// <summary>
/// HV-14 bước 1–3 — tính nguy cơ bỏ học cho HV đang học (× lớp) rồi mở / cập nhật trường hợp chăm sóc (<c>care_cases</c>):
/// có dấu hiệu mà chưa có case → mở mới; đã có → <see cref="CareCase.Reassess"/> (E2 giữ case khi hết dấu hiệu, E3 mở lại khi có dấu hiệu mới).
/// Chuyên cần đọc qua <c>Academic.Contracts.IAttendanceStats</c>.
/// Ngưỡng lấy từ Cài đặt chung (<c>atRisk.*</c>, System.Contracts.ICenterSettings — CD-03).
/// ⏳ Bài tập / điểm thi (giai đoạn 20), nợ quá hạn (Finance — giai đoạn 19) tạm tính 0.
/// </summary>
public sealed class RiskCalculator(IStudentsDbContext db, IAttendanceStats attendance, ICenterSettings settings, IClock clock)
{
    /// <param name="classIds">null = mọi lớp có HV đang học</param>
    /// <param name="studentIds">null = mọi HV đang học của các lớp đó</param>
    /// <returns>Số HV × lớp đang có dấu hiệu</returns>
    public async Task<int> RecalculateAsync(IReadOnlyCollection<Guid>? classIds, IReadOnlyCollection<Guid>? studentIds, CancellationToken ct)
    {
        var query = db.Enrollments.IgnoreQueryFilters().Where(e => e.Status == EnrollmentStatus.Studying);
        if (classIds is not null) query = query.Where(e => classIds.Contains(e.ClassId));
        if (studentIds is not null)
        {
            var sids = studentIds.Select(s => new StudentId(s)).ToList();
            query = query.Where(e => sids.Contains(e.StudentId));
        }
        var studying = await query.Select(e => new { e.StudentId, e.ClassId, e.BranchId }).Distinct().ToListAsync(ct);
        if (studying.Count == 0) return 0;

        var classes = studying.Select(e => e.ClassId).Distinct().ToList();
        var stats = (await attendance.ForClassesAsync(classes, studentIds, ct)).ToDictionary(s => (s.ClassId, s.StudentId));
        var students = studying.Select(e => e.StudentId).Distinct().ToList();
        var cases = (await db.CareCases.IgnoreQueryFilters().Where(c => classes.Contains(c.ClassId) && students.Contains(c.StudentId)).ToListAsync(ct))
            .ToDictionary(c => (c.ClassId, c.StudentId.Value));

        var t = (await settings.GetAsync(ct)).AtRisk;
        var thresholds = new RiskThresholds(t.Absent, t.Streak, t.AttendanceRate, t.SubmitRate, t.ScorePct, t.MediumFrom, t.HighFrom);
        var flagged = 0;
        var now = clock.UtcNow;
        foreach (var e in studying)
        {
            var risk = RiskAssessment.Evaluate(Signals(stats.GetValueOrDefault((e.ClassId, e.StudentId.Value))), thresholds);
            if (risk.Flagged) flagged++;
            if (cases.TryGetValue((e.ClassId, e.StudentId.Value), out var existing)) existing.Reassess(risk, now);
            else if (risk.Flagged)
            {
                var created = CareCase.Detect(e.StudentId, e.ClassId, e.BranchId, risk, now);
                db.CareCases.Add(created);
                cases[(e.ClassId, e.StudentId.Value)] = created;
            }
        }
        await db.SaveChangesAsync(ct);
        return flagged;
    }

    private static RiskSignals Signals(AttendanceStat? s) =>
        new(s?.Taken ?? 0, s?.Absent ?? 0, s?.Streak ?? 0, s?.AttendanceRate, Due: 0, Submitted: 0, AssignmentAvgPct: null, WeakExams: 0,
            OverdueDebt: false);
}
