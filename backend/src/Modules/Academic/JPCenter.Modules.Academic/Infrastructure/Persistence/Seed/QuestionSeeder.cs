using JPCenter.Application.Common.Files;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Seed;

/// <summary>
/// Nạp Ngân hàng câu hỏi mẫu (user story 11) từ <c>questions.seed.json</c> (xuất từ <c>mocks/data/questions.ts</c>) — chỉ khi bảng còn trống.
/// Đi qua đúng luồng domain (tạo → gửi duyệt → duyệt / trả lại → ngừng dùng) và dựng lại thống kê lượt làm.
/// Bài học mock "CUR-N5-V2-L3" = bài thứ 3 của lộ trình. File nghe: 1 file mp3 mẫu dùng chung (kho không chạy → chỉ lưu key).
/// Mock có câu người soạn = người duyệt (tổ trưởng) — domain cấm tự duyệt nên đổi người duyệt sang GV khác.
/// </summary>
internal sealed class QuestionSeeder(AcademicDbContext db, IFileStorage storage, IClock clock, ILogger<QuestionSeeder> logger) : IDataSeeder
{
    public int Order => 33;

    /// <summary>Người duyệt thay khi mock ghi người duyệt = người soạn (tổ trưởng / GV khác)</summary>
    private static readonly string[] FallbackReviewers = ["t-ha", "t-long"];

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.Questions.IgnoreQueryFilters().AnyAsync(ct)) return;
        var data = SeedData.Read<QuestionSeedFile>(typeof(QuestionSeeder).Assembly, "questions.seed.json");

        var curriculumIds = data.Questions.Where(q => q.CurriculumId is not null).Select(q => new CurriculumId(SeedData.IdOf(q.CurriculumId!))).Distinct().ToList();
        var lessons = (await db.Curricula.Where(c => curriculumIds.Contains(c.Id)).ToListAsync(ct))
            .ToDictionary(c => c.Id, c => c.Lessons.OrderBy(l => l.Order).Select(l => l.Id).ToList());
        StoredFile? audio = null;
        var now = clock.UtcNow;
        var count = 0;

        foreach (var s in data.Questions)
        {
            CurriculumId? curriculum = s.CurriculumId is null ? null : new CurriculumId(SeedData.IdOf(s.CurriculumId));
            var lessonIds = new List<LessonId>();
            if (curriculum is { } cid && lessons.TryGetValue(cid, out var list))
                foreach (var mock in s.LessonIds)
                    if (int.TryParse(mock[(mock.LastIndexOf("-L", StringComparison.Ordinal) + 2)..], out var n) && n >= 1 && n <= list.Count)
                        lessonIds.Add(list[n - 1]);
            if (curriculum is not null && !lessons.ContainsKey(curriculum.Value)) curriculum = null;

            var content = new QuestionContent(Enum.Parse<JlptLevel>(s.Level), SnakeCaseEnumConverter<JapaneseSkill>.Parse(s.Skill),
                SnakeCaseEnumConverter<QuestionFormat>.Parse(s.Format ?? "choice"), SnakeCaseEnumConverter<QuestionDifficulty>.Parse(s.Difficulty),
                s.Stem, s.Passage, s.Transcript, s.Options, s.Answer, s.Explanation, curriculum, lessonIds, s.Tags ?? []);
            var author = s.AuthorId is null ? Guid.Empty : SeedData.IdOf(s.AuthorId);
            var created = Question.Create(s.Code, content, author);
            if (created.IsFailure)
            {
                logger.LogWarning("Bỏ qua câu hỏi mẫu {Code}: {Error}", s.Code, created.Error.Message);
                continue;
            }
            var q = created.Value;

            if (s.Audio is { } a && q.Skill == JapaneseSkill.Listening)
            {
                audio ??= await DemoAudioAsync(ct);
                q.SetAudio(audio.Key, a.Name, Math.Clamp(a.DurationSec, 1, Question.MaxAudioSeconds));
            }

            var fallback = FallbackReviewers.Select(SeedData.IdOf).First(id => id != author);
            var reviewer = s.Review?.ById is { } rid && SeedData.IdOf(rid) != author ? SeedData.IdOf(rid) : fallback;
            var at = s.Review?.At ?? now;
            switch (s.Status)
            {
                case "draft" when s.Review?.Note is { Length: > 0 } note:
                    q.Submit();
                    q.Return(reviewer, at, note);
                    break;
                case "review":
                    q.Submit();
                    break;
                case "approved":
                    q.Submit();
                    q.Approve(reviewer, at);
                    break;
                case "retired":
                    q.Submit();
                    q.Approve(fallback, at);
                    q.Retire(reviewer, at, s.Review?.Note ?? "Ngừng dùng");
                    break;
            }

            if (s.Stats is { } st)
            {
                for (var i = 0; i < st.Used; i++) q.RecordUse();
                var picks = st.Picks ?? [];
                for (var k = 0; k < picks.Count; k++)
                    for (var i = 0; i < picks[k]; i++) q.RecordAnswer(k);
                for (var i = picks.Sum(); i < st.Attempts; i++) q.RecordAnswer(-1); // không chọn đáp án
            }

            db.AddWithId(q, SeedData.IdOf(s.Id));
            count++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Dữ liệu mẫu ngân hàng câu hỏi: {Count} câu", count);
    }

    /// <summary>1 file mp3 mẫu (đầu ID3) cho mọi câu nghe hiểu mẫu</summary>
    private async Task<StoredFile> DemoAudioAsync(CancellationToken ct)
    {
        const string name = "cau-nghe-mau.mp3";
        byte[] bytes = [.. "ID3"u8, 0x04, .. new byte[60]];
        var fallback = new StoredFile($"academic/questions/demo/{name}", name, "audio/mpeg", bytes.Length);
        try
        {
            var r = await storage.UploadAsync(new MemoryStream(bytes), name, "academic/questions/demo", FilePolicy.Audio, ct);
            return r.IsSuccess ? r.Value : fallback;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning("Không tải được file nghe mẫu lên kho ({Message}) — câu nghe mẫu chỉ có key", e.Message);
            return fallback;
        }
    }

    private sealed record QuestionSeedFile(List<SeedQuestion> Questions);

    private sealed record SeedQuestion(string Id, string Code, string Level, string Skill, string? Format, string Difficulty, string Stem, string? Passage,
        SeedAudio? Audio, string? Transcript, List<string> Options, int Answer, string? Explanation, string? CurriculumId, List<string> LessonIds,
        List<string>? Tags, string Status, string? AuthorId, SeedReview? Review, SeedStats? Stats);

    private sealed record SeedAudio(string Name, int DurationSec);

    private sealed record SeedReview(string? ById, DateTimeOffset? At, string? Note);

    private sealed record SeedStats(int Used, int Attempts, int Correct, List<int>? Picks);
}
