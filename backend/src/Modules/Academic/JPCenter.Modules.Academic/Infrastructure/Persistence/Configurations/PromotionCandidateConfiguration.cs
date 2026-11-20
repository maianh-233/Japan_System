using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// academic.promotion_candidates (+ promotion_notes) — database.md bảng 56, 57. UQ (from_class_id, student_id): 1 HV / lớp 1 đề xuất.
/// </summary>
internal sealed class PromotionCandidateConfiguration : IEntityTypeConfiguration<PromotionCandidate>
{
    public void Configure(EntityTypeBuilder<PromotionCandidate> b)
    {
        b.ToTable("promotion_candidates");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.FromClassId, x.StudentId }).IsUnique();
        b.HasIndex(x => new { x.ToClassId, x.SeatHeld });
        b.HasIndex(x => new { x.SaleId, x.Status });
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.FromClassId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Class>().WithMany().HasForeignKey(x => x.ToClassId).OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.DeclineReason).HasMaxLength(500);
        b.Ignore(x => x.IsClosed);
        b.Ignore(x => x.IsHandedOver);

        b.OwnsMany(x => x.Notes, n =>
        {
            n.ToTable("promotion_notes");
            n.WithOwner().HasForeignKey("PromotionCandidateId");
            n.Property<PromotionCandidateId>("PromotionCandidateId");
            n.HasKey(x => x.Id);
            n.Property(x => x.Id).ValueGeneratedNever();
            n.HasIndex("PromotionCandidateId");
            n.Property(x => x.Text).HasMaxLength(2000);
            n.Property(x => x.Channel).HasMaxLength(20);
            n.Property(x => x.ByName).HasMaxLength(200);
        });
        b.Navigation(x => x.Notes).HasField("_notes").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
