using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>academic.leave_requests — database.md bảng 59. session_id / makeup_session_id không ràng buộc FK (như đơn hủy buổi).</summary>
internal sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.ToTable("leave_requests");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SessionId, x.StudentId });
        b.HasIndex(x => new { x.StudentId, x.Status });
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.HasIndex(x => x.MakeupSessionId);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.ReasonType).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.ByName).HasMaxLength(200);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Ignore(x => x.IsPending);
    }
}
