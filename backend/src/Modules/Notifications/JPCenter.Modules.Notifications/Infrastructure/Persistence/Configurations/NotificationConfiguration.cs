using JPCenter.Modules.Notifications.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// notifications.notifications — database.md bảng 98. <c>recipient_id</c> không ràng buộc FK (tài khoản ở schema system).
/// Không xóa mềm: dọn hẳn bằng job (TB-05).
/// </summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(Notification.TitleMaxLength);
        b.Property(x => x.Description).HasMaxLength(Notification.DescriptionMaxLength);
        b.Property(x => x.Link).HasMaxLength(Notification.LinkMaxLength);
        b.Property(x => x.EventKey).HasMaxLength(100);

        // Chuông: 50 thông báo mới nhất của 1 người (TB-01)
        b.HasIndex(x => new { x.RecipientType, x.RecipientId, x.CreatedAt }).IsDescending(false, false, true);
        // Đếm chưa đọc / đọc hết (TB-02)
        b.HasIndex(x => new { x.RecipientType, x.RecipientId }).HasFilter("read_at IS NULL").HasDatabaseName("ix_notifications_unread");
        // Job dọn (TB-05)
        b.HasIndex(x => x.CreatedAt);
    }
}
