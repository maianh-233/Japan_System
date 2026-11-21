using JPCenter.Modules.Crm.Domain.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Configurations;

/// <summary>crm.message_templates — database.md bảng 23.</summary>
internal sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> b)
    {
        b.ToTable("message_templates");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Scenario);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Body).HasMaxLength(2000);
    }
}
