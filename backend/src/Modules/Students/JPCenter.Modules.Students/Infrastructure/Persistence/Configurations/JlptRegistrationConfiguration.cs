using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Configurations;

/// <summary>
/// students.jlpt_registrations — database.md bảng 31. 1 HV chỉ 1 lượt chưa hủy trong 1 kỳ (HV-16.E9 — UQ có điều kiện).
/// Chứng chỉ gốc là owned cùng bảng, cột <c>cert_*</c>.
/// </summary>
internal sealed class JlptRegistrationConfiguration : IEntityTypeConfiguration<JlptRegistration>
{
    public void Configure(EntityTypeBuilder<JlptRegistration> b)
    {
        b.ToTable("jlpt_registrations");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique().HasFilter("status <> 'cancelled'");
        b.HasIndex(x => new { x.BranchId, x.Status });
        b.HasIndex(x => x.StudentId);
        b.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<JlptSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Venue).HasMaxLength(200);
        b.Property(x => x.AdmitNo).HasMaxLength(50);
        b.Property(x => x.Note).HasMaxLength(JlptRegistration.MaxNote);
        b.Ignore(x => x.HasAllDocuments);

        b.OwnsOne(x => x.Certificate, c =>
        {
            c.Property(x => x.No).HasColumnName("cert_no").HasMaxLength(50);
            c.Property(x => x.ReceivedAt).HasColumnName("cert_received_at");
            c.Property(x => x.ReceivedBy).HasColumnName("cert_received_by");
            c.Property(x => x.HandedAt).HasColumnName("cert_handed_at");
            c.Property(x => x.HandedBy).HasColumnName("cert_handed_by");
            c.Property(x => x.HandedTo).HasColumnName("cert_handed_to").HasMaxLength(200);
        });
    }
}
