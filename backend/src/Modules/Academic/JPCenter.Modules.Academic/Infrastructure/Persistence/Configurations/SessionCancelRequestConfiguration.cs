using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// academic.session_cancel_requests — database.md bảng 58.
/// session_id KHÔNG ràng buộc FK: sinh lại lịch có thể xóa buổi chưa dạy — đơn còn chờ của buổi đã mất được đóng (DT-16.E6).
/// </summary>
internal sealed class SessionCancelRequestConfiguration : IEntityTypeConfiguration<SessionCancelRequest>
{
    public void Configure(EntityTypeBuilder<SessionCancelRequest> b)
    {
        b.ToTable("session_cancel_requests");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SessionId, x.Status });
        b.HasIndex(x => new { x.TeacherId, x.Status });
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.ReasonType).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Ignore(x => x.IsPending);
    }
}
