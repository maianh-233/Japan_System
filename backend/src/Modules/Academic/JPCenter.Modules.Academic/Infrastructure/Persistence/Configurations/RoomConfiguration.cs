using JPCenter.Modules.Academic.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>academic.rooms — database.md bảng 43. Tên duy nhất trong chi nhánh (trong số chưa xóa).</summary>
internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("rooms");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BranchId, x.Name }).IsUnique().HasFilter("is_deleted = false");
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Floor).HasMaxLength(100);
        b.Property(x => x.MaintenanceReason).HasMaxLength(500);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Ignore(x => x.IsUsable);
    }
}
