using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>academic.courses (+ academic.course_curricula) — database.md bảng 35, 36.</summary>
internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("courses");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("is_deleted = false");
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Ignore(x => x.DefaultCurriculumId);

        b.OwnsMany(x => x.Curricula, l =>
        {
            l.ToTable("course_curricula");
            l.WithOwner().HasForeignKey("CourseId");
            l.Property<CourseId>("CourseId");
            l.HasKey("CourseId", nameof(CourseCurriculum.CurriculumId));
            l.HasIndex(x => x.CurriculumId);
            l.HasOne<Curriculum>().WithMany().HasForeignKey(x => x.CurriculumId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Navigation(x => x.Curricula).HasField("_curricula").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
