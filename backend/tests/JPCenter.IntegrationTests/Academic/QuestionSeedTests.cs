using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Academic;

/// <summary>Giai đoạn 17 phase 2 — Ngân hàng câu hỏi mẫu nạp đúng từ mock frontend (user story 11).</summary>
[Collection(SeededCollection.Name)]
public sealed class QuestionSeedTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ngan_hang_cau_hoi_mau()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        // Chỉ câu mẫu (Id cố định từ mock "Q-1" … "Q-36") — test khác cùng DB có thể đã thêm / sửa câu
        var seeded = Enumerable.Range(1, 36).Select(i => new QuestionId(SeedData.IdOf($"Q-{i}"))).ToList();
        var all = await db.Questions.AsNoTracking().Where(q => seeded.Contains(q.Id)).ToListAsync(Ct);

        all.Count.ShouldBe(36);
        all.GroupBy(q => q.Status).ToDictionary(g => g.Key, g => g.Count())
            .ShouldBe(new Dictionary<QuestionStatus, int>
            {
                [QuestionStatus.Approved] = 30, [QuestionStatus.Review] = 3, [QuestionStatus.Draft] = 2, [QuestionStatus.Retired] = 1,
            }, ignoreOrder: true);
        all.ShouldAllBe(q => q.Options.Count == 4 && q.AuthorId != Guid.Empty);
        all.Where(q => q.Status == QuestionStatus.Approved).ShouldAllBe(q => q.ReviewedBy != null && q.ReviewedBy != q.AuthorId);

        // Câu 1: N5 đọc Kanji, gắn bài 3 của lộ trình N5 v2, có thống kê
        var q1 = all.Single(q => q.Id == new QuestionId(SeedData.IdOf("Q-1")));
        (q1.Code, q1.Level, q1.Skill, q1.Format).ShouldBe(("N5-KA-0001", JlptLevel.N5, JapaneseSkill.Kanji, QuestionFormat.Choice));
        q1.AuthorId.ShouldBe(SeedData.IdOf("t-anh"));
        var n5 = await db.Curricula.AsNoTracking().FirstAsync(c => c.Id == q1.CurriculumId, Ct);
        q1.LessonIds.ShouldBe([n5.Lessons.OrderBy(l => l.Order).ElementAt(2).Id]);
        q1.AttemptCount.ShouldBeGreaterThan(0);
        q1.Picks.Sum().ShouldBeLessThanOrEqualTo(q1.AttemptCount); // lượt bỏ trống không vào picks (test bốc câu có thể ghi thêm)
        q1.CorrectCount.ShouldBe(q1.Picks[q1.Answer]);
        q1.UsedCount.ShouldBeGreaterThan(0);

        // Nghe hiểu có file nghe + lời thoại; ngừng dùng có lý do
        all.Where(q => q.Skill == JapaneseSkill.Listening).ShouldAllBe(q => q.AudioKey != null && q.AudioName != null && q.Transcript != null);
        all.Single(q => q.Status == QuestionStatus.Retired).ReviewNote!.ShouldStartWith("Lộ đề");
        all.Where(q => q.Format == QuestionFormat.Star).ShouldAllBe(q => q.Stem.Contains('★'));
    }
}
