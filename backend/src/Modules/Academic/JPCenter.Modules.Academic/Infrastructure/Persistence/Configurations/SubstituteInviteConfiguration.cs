using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>academic.substitute_invites — database.md bảng 60. session_id không ràng buộc FK (như đơn hủy buổi).</summary>
internal sealed class SubstituteInviteConfiguration : IEntityTypeConfiguration<SubstituteInvite>
{
    public void Configure(EntityTypeBuilder<SubstituteInvite> b)
    {
        b.ToTable("substitute_invites");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SessionId, x.Status });
        b.HasIndex(x => new { x.ToTeacherId, x.Status });
        b.HasIndex(x => x.CancelRequestId);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ClassId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<SessionCancelRequest>().WithMany().HasForeignKey(x => x.CancelRequestId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.DeclineReason).HasMaxLength(500);
        b.Ignore(x => x.IsPending);
    }
}
