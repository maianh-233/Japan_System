using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Configurations;

/// <summary>students.abroad_cases (+ abroad_documents, abroad_notes) — database.md bảng 32 → 34. Người phụ trách → hr.employees, không FK khác schema.</summary>
internal sealed class AbroadCaseConfiguration : IEntityTypeConfiguration<AbroadCase>
{
    public void Configure(EntityTypeBuilder<AbroadCase> b)
    {
        b.ToTable("abroad_cases");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.BranchId, x.Stage });
        b.HasIndex(x => x.StudentId);
        b.HasIndex(x => x.AssigneeId);
        b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Partner).HasMaxLength(AbroadCase.MaxText);
        b.Property(x => x.Destination).HasMaxLength(AbroadCase.MaxText);
        b.Property(x => x.Job).HasMaxLength(AbroadCase.MaxText);
        b.Property(x => x.Intake).HasMaxLength(7);
        b.Property(x => x.CancelReason).HasMaxLength(500);
        b.Ignore(x => x.IsCancelled);
        b.Ignore(x => x.AllDocumentsVerified);

        b.OwnsMany(x => x.Documents, d =>
        {
            d.ToTable("abroad_documents");
            d.WithOwner().HasForeignKey("AbroadCaseId");
            d.Property<AbroadCaseId>("AbroadCaseId");
            d.HasKey(x => x.Id);
            d.Property(x => x.Id).ValueGeneratedNever();
            d.Property(x => x.Name).HasMaxLength(200);
            d.HasIndex("AbroadCaseId");
        });
        b.Navigation(x => x.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Notes, n =>
        {
            n.ToTable("abroad_notes");
            n.WithOwner().HasForeignKey("AbroadCaseId");
            n.Property<AbroadCaseId>("AbroadCaseId");
            n.HasKey(x => x.Id);
            n.Property(x => x.Id).ValueGeneratedNever();
            n.Property(x => x.Text).HasMaxLength(AbroadCase.MaxNote);
            n.Property(x => x.ByName).HasMaxLength(200);
            n.HasIndex("AbroadCaseId");
        });
        b.Navigation(x => x.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
