using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Configurations;

/// <summary>hr.candidates + candidate_notes — database.md bảng 86, 87. Người phỏng vấn không FK (giữ lịch sử khi người đó nghỉ).</summary>
internal sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> b)
    {
        b.ToTable("candidates");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.JobId, x.Stage });
        b.HasIndex(x => x.InterviewAt);
        b.HasOne<Job>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.Phone).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Source).HasMaxLength(100);
        b.Property(x => x.CvKey).HasMaxLength(500);
        b.Property(x => x.CvName).HasMaxLength(300);
        b.Property(x => x.Japanese).HasMaxLength(100);
        b.Property(x => x.InterviewNote).HasMaxLength(2000);
        b.Property(x => x.RejectReason).HasMaxLength(500);
        b.Property(x => x.InterviewScore).HasPrecision(4, 2);
        b.Ignore(x => x.IsClosed);

        b.OwnsMany(x => x.Notes, n =>
        {
            n.ToTable("candidate_notes");
            n.WithOwner().HasForeignKey("CandidateId");
            n.Property<CandidateId>("CandidateId");
            n.HasKey(x => x.Id);
            n.Property(x => x.Id).ValueGeneratedNever();
            n.HasIndex("CandidateId");
            n.Property(x => x.Text).HasMaxLength(2000);
            n.Property(x => x.ByName).HasMaxLength(200);
        });
        b.Navigation(x => x.Notes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
