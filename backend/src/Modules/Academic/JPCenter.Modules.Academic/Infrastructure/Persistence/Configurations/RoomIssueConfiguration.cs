using JPCenter.Modules.Academic.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>academic.room_issues — database.md bảng 44.</summary>
internal sealed class RoomIssueConfiguration : IEntityTypeConfiguration<RoomIssue>
{
    public void Configure(EntityTypeBuilder<RoomIssue> b)
    {
        b.ToTable("room_issues");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.RoomId, x.Status });
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Title).HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Assignee).HasMaxLength(200);
        b.Property(x => x.Resolution).HasMaxLength(2000);
    }
}
