using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Announcements.Common;

/// <summary>
/// Gửi thông báo lớp (ND-07) — ngay khi tạo hoặc tới giờ hẹn: người nhận = HV đang học (lọc HV được chọn, chỉ HV nợ học phí quá hạn — Finance.Contracts)
/// → HV / PH của họ / GV + trợ giảng của lớp theo "gửi tới". Kênh cổng = chuông + trang thông báo; ⏳ Zalo / email: giai đoạn 22 (chỉ ghi nhận).
/// Không có người nhận → E1. Gọi trước <c>SaveChangesAsync</c>.
/// </summary>
public sealed class AnnouncementSender(IAcademicDbContext db, LearningAccess access, LearningNotices notices, ITuitionQuery tuition)
{
    public async Task<Result> SendAsync(ClassAnnouncement a, DateTimeOffset now, CancellationToken ct)
    {
        var cls = await db.Classes.AsNoTracking().IgnoreQueryFilters().FirstAsync(c => c.Id == a.ClassId, ct);
        var students = (await access.StudyingAsync(cls.Id, ct)).ToList();
        if (a.StudentIds.Count > 0) students = students.Where(a.StudentIds.Contains).ToList();
        if (a.DebtorsOnly)
        {
            var overdue = await tuition.OverdueStudentsAsync(students, ct);
            students = students.Where(overdue.Contains).ToList();
        }

        var reach = new LearningReach(
            a.Audience.Contains(AnnouncementAudience.Student) ? students : [],
            a.Audience.Contains(AnnouncementAudience.Parent) ? await notices.GuardiansOfAsync(students, ct) : [],
            a.Audience.Contains(AnnouncementAudience.Teacher) ? [.. cls.TeacherIds.Union(cls.AssistantIds)] : []);
        var sent = a.MarkSent(reach.People, now);
        if (sent.IsFailure) return sent;
        if (a.Channels.Contains(AnnouncementChannel.Portal))
            await notices.SendAsync(reach, $"{cls.Code}: {a.Title}", Preview(a.Content), "info", "academic.announcement.sent", "/student/dashboard",
                "/parent/dashboard", "/teacher/classes", ct);
        return Result.Success();
    }

    private static string Preview(string content) => content.Length <= 200 ? content : content[..197] + "...";
}
