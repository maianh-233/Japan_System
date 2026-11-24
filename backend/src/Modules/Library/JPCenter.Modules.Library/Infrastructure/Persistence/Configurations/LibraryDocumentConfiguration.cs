using JPCenter.Modules.Library.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Library.Infrastructure.Persistence.Configurations;

/// <summary>
/// library.documents (database.md 64 — levels / skills / audience / tags text[], branch_ids uuid[] → system.branches, không FK khác schema)
/// + document_views (65 — chỉ thêm; thống kê theo tài liệu × thời gian, người đọc theo loại × id).
/// </summary>
internal sealed class LibraryDocumentConfiguration : IEntityTypeConfiguration<LibraryDocument>
{
    public void Configure(EntityTypeBuilder<LibraryDocument> b)
    {
        b.ToTable("documents");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.Status, x.Category });
        b.HasIndex(x => x.Levels).HasMethod("gin");
        b.HasIndex(x => x.BranchIds).HasMethod("gin");
        b.Property(x => x.Title).HasMaxLength(LibraryDocument.MaxTitle);
        b.Property(x => x.Subtitle).HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.FileKey).HasMaxLength(1000);
        b.Property(x => x.FileName).HasMaxLength(300);
        b.Property(x => x.Publisher).HasMaxLength(200);
        b.Property(x => x.AvgMinutes).HasPrecision(8, 1);
        b.Ignore(x => x.IsStoredFile);
    }
}
