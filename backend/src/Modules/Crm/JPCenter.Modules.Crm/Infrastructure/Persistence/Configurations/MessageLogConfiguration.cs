using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Configurations;

/// <summary>crm.message_logs — database.md bảng 24 (chỉ thêm). <c>student_id</c>, <c>sent_by</c> không FK (schema khác).</summary>
internal sealed class MessageLogConfiguration : IEntityTypeConfiguration<MessageLog>
{
    public void Configure(EntityTypeBuilder<MessageLog> b)
    {
        b.ToTable("message_logs");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.LeadId, x.SentAt });
        b.HasIndex(x => new { x.StudentId, x.SentAt });
        b.HasIndex(x => x.Status);
        b.Property(x => x.ToName).HasMaxLength(200);
        b.Property(x => x.ToPhone).HasMaxLength(200);
        b.Property(x => x.Body).HasMaxLength(4000);
        b.HasOne<MessageTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
    }
}
