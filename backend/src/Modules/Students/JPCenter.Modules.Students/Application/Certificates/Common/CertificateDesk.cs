using JPCenter.Application.Common.Email;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Students.Application.Certificates.Common;

/// <summary>
/// HV-15 dùng chung: xét điều kiện (chuyên cần + bài thi cuối qua Academic.Contracts, còn nợ của ghi danh qua Finance.Contracts,
/// ngưỡng từ Cài đặt chung <c>atRisk</c>), sinh mã <c>CN-{năm}-{lớp}-{số:4}</c> (đếm trong năm, max + 1), gửi email có link tra cứu.
/// Bài thi cuối = kết quả thi gần nhất của HV trong lớp (Academic chưa đánh dấu "thi cuối khóa").
/// </summary>
public sealed class CertificateDesk(IStudentsDbContext db, IAttendanceStats attendance, ILearningStats learning, ITuitionQuery tuition,
    ICenterSettings settings, IEmailSender email, IAppLinks links, ILogger<CertificateDesk> logger)
{
    public const string FinalExamTitle = "Bài thi cuối khóa";

    public async Task<IReadOnlyDictionary<EnrollmentId, IReadOnlyList<CertificateCheck>>> EvaluateAsync(IReadOnlyCollection<Enrollment> list,
        CancellationToken ct)
    {
        if (list.Count == 0) return new Dictionary<EnrollmentId, IReadOnlyList<CertificateCheck>>();
        var classIds = list.Select(e => e.ClassId).Distinct().ToList();
        var studentIds = list.Select(e => e.StudentId.Value).Distinct().ToList();
        var att = (await attendance.ForClassesAsync(classIds, studentIds, ct)).ToDictionary(a => (a.ClassId, a.StudentId));
        var learn = (await learning.ForClassesAsync(classIds, studentIds, ct)).ToDictionary(a => (a.ClassId, a.StudentId));
        var debt = await tuition.DebtByEnrollmentAsync(list.Select(e => e.Id.Value).ToList(), ct);
        var t = (await settings.GetAsync(ct)).AtRisk;
        var thresholds = new RiskThresholds(t.Absent, t.Streak, t.AttendanceRate, t.SubmitRate, t.ScorePct, t.MediumFrom, t.HighFrom);
        return list.ToDictionary(e => e.Id, e =>
        {
            var key = (e.ClassId, e.StudentId.Value);
            return CertificateEligibility.Evaluate(e.Status, Facts(att.GetValueOrDefault(key), learn.GetValueOrDefault(key),
                debt.GetValueOrDefault(e.Id.Value)), thresholds);
        });
    }

    /// <summary>Số thứ tự tiếp theo trong năm (mã đã cấp, kể cả HV đã ẩn)</summary>
    public async Task<string> NextCodeAsync(int year, string classCode, CancellationToken ct)
    {
        var prefix = $"CN-{year}-";
        var codes = await db.Enrollments.IgnoreQueryFilters().Where(e => e.Certificate != null && e.Certificate.Code.StartsWith(prefix))
            .Select(e => e.Certificate!.Code).ToListAsync(ct);
        var max = codes.Select(c => int.TryParse(c[^4..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return CertificateCode.Build(year, classCode, max + 1);
    }

    /// <summary>Gửi email chứng nhận (link tra cứu công khai). Lỗi gửi → false, chứng nhận vẫn giữ (HV-15.E4)</summary>
    public async Task<bool> SendAsync(string to, string studentName, string? classCode, string? className, Certificate c, CancellationToken ct)
    {
        var verify = links.Page($"/verify/{Uri.EscapeDataString(c.Code)}");
        var content = EmailContent.Create($"Chứng nhận hoàn thành khóa học — {c.Code}", "Chứng nhận hoàn thành khóa học",
                $"Chúc mừng {studentName} đã hoàn thành lớp {classCode}")
            .Hello(studentName)
            .Text($"Trung tâm xin chúc mừng bạn đã hoàn thành **{className ?? classCode}**. Chứng nhận của bạn có mã tra cứu bên dưới.")
            .Code(c.Code, "Mã tra cứu chứng nhận")
            .Details(("Học viên", studentName), ("Lớp", classCode ?? "—"), ("Ngày cấp", c.IssuedAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy", global::System.Globalization.CultureInfo.InvariantCulture)))
            .Button("Xem & xác thực chứng nhận", verify)
            .Footnote("Bên thứ ba có thể kiểm tra chứng nhận bằng mã tra cứu hoặc mã QR trên chứng nhận.");
        try
        {
            await email.SendAsync(to, content, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Gửi email chứng nhận {Code} tới {To} thất bại", c.Code, to);
            return false;
        }
    }

    private static CertificateFacts Facts(AttendanceStat? a, LearningStat? l, decimal debt)
    {
        var last = l is { ExamPercents.Count: > 0 } ? l.ExamPercents[^1] : null;
        var hasExam = l is { ExamPercents.Count: > 0 };
        return new CertificateFacts(a?.AttendanceRate, hasExam ? FinalExamTitle : null, last, hasExam && last is null, Money.FromTrusted(debt));
    }
}
