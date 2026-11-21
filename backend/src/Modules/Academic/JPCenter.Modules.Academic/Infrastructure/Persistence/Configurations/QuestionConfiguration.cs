using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// academic.questions — database.md bảng 42. Đáp án / bài học / thẻ / thống kê chọn đáp án lưu mảng PostgreSQL (text[], uuid[], int[]).
/// <c>author_id</c>, <c>reviewed_by</c> không ràng buộc FK (nhân viên ở schema hr).
/// </summary>
internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> b)
    {
        b.ToTable("questions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        // Lọc / bốc câu: trạng thái + cấp độ + kỹ năng
        b.HasIndex(x => new { x.Status, x.Level, x.Skill });
        b.HasIndex(x => x.AuthorId);
        b.HasIndex(x => x.CurriculumId);
        // Bốc câu theo bài học (lesson_ids @> …)
        b.HasIndex(x => x.LessonIds).HasMethod("gin");

        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.Stem).HasMaxLength(QuestionContent.StemMaxLength);
        b.Property(x => x.Passage).HasMaxLength(QuestionContent.PassageMaxLength);
        b.Property(x => x.Transcript).HasMaxLength(QuestionContent.TextMaxLength);
        b.Property(x => x.Explanation).HasMaxLength(QuestionContent.TextMaxLength);
        b.Property(x => x.AudioKey).HasMaxLength(500);
        b.Property(x => x.AudioName).HasMaxLength(255);
        b.Property(x => x.ReviewNote).HasMaxLength(1000);

        b.HasOne<Curriculum>().WithMany().HasForeignKey(x => x.CurriculumId).OnDelete(DeleteBehavior.Restrict);

        b.Ignore(x => x.HasAudio);
        b.Ignore(x => x.CorrectRate);
    }
}
