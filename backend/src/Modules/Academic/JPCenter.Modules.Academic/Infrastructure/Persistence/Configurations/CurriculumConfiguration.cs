using JPCenter.Modules.Academic.Domain.Curricula;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// academic.curricula (+ lessons → chapters → materials, exam_milestones) — database.md bảng 37 → 41.
/// Nội dung là con của aggregate lộ trình (owned): lưu / nạp cùng lộ trình.
/// </summary>
internal sealed class CurriculumConfiguration : IEntityTypeConfiguration<Curriculum>
{
    public void Configure(EntityTypeBuilder<Curriculum> b)
    {
        b.ToTable("curricula");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.Code, x.Version }).IsUnique().HasFilter("is_deleted = false");
        b.HasIndex(x => x.PreviousVersionId);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Version).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Textbook).HasMaxLength(300);
        b.Property(x => x.EntryRequirement).HasMaxLength(500);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Ignore(x => x.IsDraft);
        b.Ignore(x => x.AllMaterials);

        b.OwnsMany(x => x.Lessons, l =>
        {
            l.ToTable("lessons");
            l.WithOwner().HasForeignKey("CurriculumId");
            l.Property<CurriculumId>("CurriculumId");
            l.HasKey(x => x.Id);
            l.Property(x => x.Id).ValueGeneratedNever();
            l.Property(x => x.Title).HasMaxLength(300);
            l.HasIndex("CurriculumId");

            l.OwnsMany(x => x.Chapters, c =>
            {
                c.ToTable("chapters");
                c.WithOwner().HasForeignKey("LessonId");
                c.Property<LessonId>("LessonId");
                c.HasKey(x => x.Id);
                c.Property(x => x.Id).ValueGeneratedNever();
                c.Property(x => x.Title).HasMaxLength(300);
                c.HasIndex("LessonId");

                c.OwnsMany(x => x.Materials, m =>
                {
                    m.ToTable("materials");
                    m.WithOwner().HasForeignKey("ChapterId");
                    m.Property<ChapterId>("ChapterId");
                    m.HasKey(x => x.Id);
                    m.Property(x => x.Id).ValueGeneratedNever();
                    m.Property(x => x.Name).HasMaxLength(300);
                    m.Property(x => x.Url).HasMaxLength(2000);
                    m.Property(x => x.FileKey).HasMaxLength(500);
                    m.Property(x => x.FileName).HasMaxLength(300);
                    m.Property(x => x.ContentType).HasMaxLength(200);
                    m.Ignore(x => x.IsFile);
                    m.HasIndex("ChapterId");
                    m.HasIndex(x => x.FileKey);
                });
                c.Navigation(x => x.Materials).HasField("_materials").UsePropertyAccessMode(PropertyAccessMode.Field);
            });
            l.Navigation(x => x.Chapters).HasField("_chapters").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        b.Navigation(x => x.Lessons).HasField("_lessons").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Exams, e =>
        {
            e.ToTable("exam_milestones");
            e.WithOwner().HasForeignKey("CurriculumId");
            e.Property<CurriculumId>("CurriculumId");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasIndex("CurriculumId");
        });
        b.Navigation(x => x.Exams).HasField("_exams").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
