using JPCenter.Modules.Students.Domain.CareCases;

namespace JPCenter.Modules.Students.Domain.Enrollments;

/// <summary>
/// HV-15 — checklist điều kiện cấp chứng nhận (giống frontend <c>certificateChecks</c>): hoàn thành lớp · chuyên cần ≥ ngưỡng ·
/// bài thi cuối ≥ ngưỡng (nếu có) · hết nợ học phí. Ngưỡng lấy từ Cài đặt chung <c>atRisk</c> (<see cref="RiskThresholds"/>).
/// </summary>
public static class CertificateEligibility
{
    public static IReadOnlyList<CertificateCheck> Evaluate(EnrollmentStatus status, CertificateFacts facts, RiskThresholds thresholds)
    {
        var rate = facts.AttendanceRate ?? 0;
        var exam = facts.FinalExamTitle switch
        {
            null => new CertificateCheck("final_exam", true, "Không có bài thi bắt buộc"),
            var t when facts.FinalExamAbsent => new CertificateCheck("final_exam", false, $"{t} ≥ {thresholds.ScorePct}% (vắng thi)"),
            var t => new CertificateCheck("final_exam", facts.FinalExamPct >= thresholds.ScorePct,
                $"{t} ≥ {thresholds.ScorePct}% ({(facts.FinalExamPct is { } p ? $"{p:0.#}%" : "chưa có điểm")})"),
        };
        return
        [
            new("completed", status == EnrollmentStatus.Completed, "Lớp đã kết thúc / học viên đã hoàn thành"),
            new("attendance", rate >= thresholds.AttendanceRate, $"Chuyên cần ≥ {thresholds.AttendanceRate}% (hiện {rate}%)"),
            exam,
            new("tuition", facts.Debt.Amount <= 0, facts.Debt.Amount > 0 ? $"Hoàn tất học phí (còn nợ {facts.Debt})" : "Hoàn tất học phí"),
        ];
    }

    public static bool IsEligible(IReadOnlyList<CertificateCheck> checks) => checks.All(c => c.Ok);
}
