using JPCenter.Modules.Library.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Library.Infrastructure.Persistence.Configurations;

/// <summary>library.document_views — database.md 65. Lượt xem chỉ thêm; người xem (nhân viên / HV / PH) không FK khác schema.</summary>
internal sealed class DocumentViewConfiguration : IEntityTypeConfiguration<DocumentView>
{
    public void Configure(EntityTypeBuilder<DocumentView> b)
    {
        b.ToTable("document_views");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.DocumentId, x.StartedAt });
        b.HasIndex(x => new { x.ViewerType, x.ViewerId });
        b.HasOne<LibraryDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.ViewerName).HasMaxLength(200);
        b.Property(x => x.ViewerRole).HasMaxLength(100);
        b.Property(x => x.Minutes).HasPrecision(6, 1);
    }
}
