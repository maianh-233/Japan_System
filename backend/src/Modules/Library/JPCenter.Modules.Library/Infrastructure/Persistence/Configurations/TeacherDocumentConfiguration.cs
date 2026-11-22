using JPCenter.Modules.Library.Domain.TeacherDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Library.Infrastructure.Persistence.Configurations;

/// <summary>
/// library.teacher_documents (database.md 66 — class_ids uuid[] → academic.classes, session_id → academic.class_sessions, không FK khác schema)
/// + teacher_document_views (67, khóa (teacher_document_id, student_id)).
/// </summary>
internal sealed class TeacherDocumentConfiguration : IEntityTypeConfiguration<TeacherDocument>
{
    public void Configure(EntityTypeBuilder<TeacherDocument> b)
    {
        b.ToTable("teacher_documents");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.TeacherId);
        b.HasIndex(x => x.ClassIds).HasMethod("gin");
        b.Property(x => x.Title).HasMaxLength(TeacherDocument.MaxTitle);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.FileName).HasMaxLength(300);
        b.Property(x => x.Url).HasMaxLength(1000);
        b.Ignore(x => x.IsStoredFile);
        b.OwnsMany(x => x.Views, v =>
        {
            v.ToTable("teacher_document_views");
            v.WithOwner().HasForeignKey("TeacherDocumentId");
            v.Property<TeacherDocumentId>("TeacherDocumentId");
            v.HasKey("TeacherDocumentId", nameof(TeacherDocumentView.StudentId));
            // Khóa do nghiệp vụ đặt (HV) — lượt xem mới thêm vào tài liệu đang theo dõi phải là INSERT
            v.Property(x => x.StudentId).ValueGeneratedNever();
        });
        b.Navigation(x => x.Views).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
