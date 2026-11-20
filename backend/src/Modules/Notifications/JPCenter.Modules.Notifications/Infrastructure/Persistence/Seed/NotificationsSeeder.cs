using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Notifications.Domain.Notifications;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Notifications.Infrastructure.Persistence.Seed;

/// <summary>
/// Thông báo mẫu từ <c>frontend/src/mocks/data/notifications.ts</c> cho các tài khoản demo (chỉ khi bảng còn trống):
/// nhân viên → Super Admin, Admin, Giáo vụ · GV → Nguyễn Thu Hà · HV → hv1000 · PH → ph.0912345678.
/// Id tài khoản theo quy ước seed của System (<c>acc:&lt;mã NV&gt;</c>, mã tài khoản cổng).
/// </summary>
internal sealed class NotificationsSeeder(NotificationsDbContext db, IClock clock, ILogger<NotificationsSeeder> logger) : IDataSeeder
{
    public int Order => 90;

    private static readonly string[] StaffAccounts = ["acc:e-huy", "acc:e-trang", "acc:e-mai"];
    private const string TeacherAccount = "acc:t-ha";
    private const string StudentAccount = "ACC-S-ST-1";
    private const string ParentAccount = "ACC-G-1";

    private sealed record Sample(string Title, string Description, int MinutesAgo, bool Read, NotificationTone Tone, string? Link);

    private static readonly Sample[] Staff =
    [
        new("Đơn xin hủy buổi mới", "GV Nguyễn Thu Hà xin hủy buổi N3-K12 tối thứ 4.", 5, false, NotificationTone.Warning, "/admin/academic/requests"),
        new("3 lead mới từ Facebook", "Chiến dịch \"Khai giảng N5 tháng 12\".", 42, false, NotificationTone.Info, "/admin/crm/leads"),
        new("Hóa đơn quá hạn", "5 học viên quá hạn đóng học phí đợt 2.", 180, false, NotificationTone.Danger, "/admin/finance/debts"),
        new("Lớp N4-K15 đủ sĩ số", "Lớp đã đạt 20/20 học viên.", 60 * 26, true, NotificationTone.Success, null),
    ];

    private static readonly Sample[] Teacher =
    [
        new("Đơn hủy buổi đã được duyệt", "Buổi N3-K12 ngày 19/11 sẽ học bù vào thứ 7.", 15, false, NotificationTone.Success, null),
        new("4 bài tập chờ chấm", "Lớp N4-K15 · Bài 12 — Ngữ pháp ～ば.", 120, false, NotificationTone.Info, "/teacher/assignments"),
        new("Đổi phòng học", "Lớp N3-K12 chuyển sang P.302 từ tuần sau.", 60 * 30, true, NotificationTone.Warning, null),
    ];

    private static readonly Sample[] Student =
    [
        new("Bài tập mới", "Bài 12 — hạn nộp 23:59 thứ 6.", 30, false, NotificationTone.Info, "/student/assignments"),
        new("Buổi học tối thứ 4 bị hủy", "Học bù vào 09:00 thứ 7, phòng 205.", 90, false, NotificationTone.Warning, "/student/timetable"),
        new("Đã nhận học phí", "Thanh toán đợt 1: 4.500.000 ₫.", 60 * 48, true, NotificationTone.Success, null),
    ];

    private static readonly Sample[] Parent =
    [
        new("Con vắng học tối thứ 4", "Nguyễn Văn An vắng buổi N3-K12 (có phép).", 40, false, NotificationTone.Warning, "/parent/attendance"),
        new("Học phí đợt 2 sắp đến hạn", "1.500.000 ₫ — hạn 30/11.", 60 * 5, false, NotificationTone.Info, "/parent/tuition"),
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Notifications.AnyAsync(ct)) return;
        var now = clock.UtcNow;
        var count = 0;

        void Add(RecipientType type, string accountMockId, IEnumerable<Sample> samples)
        {
            foreach (var s in samples)
            {
                var at = now.AddMinutes(-s.MinutesAgo);
                var created = Notification.Create(type, SeedData.IdOf(accountMockId), s.Title, s.Description, s.Tone, s.Link, "seed", at);
                if (created.IsFailure)
                {
                    logger.LogWarning("Bỏ qua thông báo mẫu {Title}: {Error}", s.Title, created.Error.Message);
                    continue;
                }
                if (s.Read) created.Value.MarkRead(at.AddMinutes(1));
                db.Notifications.Add(created.Value);
                count++;
            }
        }

        foreach (var account in StaffAccounts) Add(RecipientType.Staff, account, Staff);
        Add(RecipientType.Staff, TeacherAccount, Teacher);
        Add(RecipientType.Portal, StudentAccount, Student);
        Add(RecipientType.Portal, ParentAccount, Parent);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dữ liệu mẫu Notifications: {Count} thông báo", count);
    }
}
