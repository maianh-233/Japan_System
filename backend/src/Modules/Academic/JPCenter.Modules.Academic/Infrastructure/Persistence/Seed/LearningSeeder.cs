using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.Modules.Academic.Domain.Flashcards;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Seed;

/// <summary>
/// Dữ liệu mẫu Nội dung học (user story 12, giai đoạn 20) từ mock frontend (learning.seed.json): bài tập + bài nộp, kết quả thi
/// (dữ liệu cũ — chưa gắn kỳ thi), thông báo lớp, đánh giá GV, flashcard + tiến độ ôn của HV demo. Mỗi nhóm chỉ nạp khi bảng trống.
/// Lượt xem thông báo mẫu ghi bằng tài khoản giả (chỉ để đếm).
/// </summary>
internal sealed class LearningSeeder(AcademicDbContext db, ILogger<LearningSeeder> logger) : IDataSeeder
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    public int Order => 34;

    public async Task SeedAsync(CancellationToken ct)
    {
        var data = SeedData.Read<LearningSeedFile>(typeof(LearningSeeder).Assembly, "learning.seed.json");
        var counts = new List<string>();
        if (!await db.Assignments.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedAssignments(data)} bài tập, {data.Submissions.Count} bài nộp");
        if (!await db.ExamResults.AnyAsync(ct)) counts.Add($"{SeedExamResults(data)} kết quả thi");
        if (!await db.ClassAnnouncements.AnyAsync(ct)) counts.Add($"{SeedAnnouncements(data)} thông báo lớp");
        if (!await db.TeacherReviews.AnyAsync(ct)) counts.Add($"{SeedReviews(data)} đánh giá GV");
        if (!await db.FlashDecks.IgnoreQueryFilters().AnyAsync(ct)) counts.Add($"{SeedDecks(data)} bộ flashcard");
        if (counts.Count == 0) return;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã nạp dữ liệu mẫu Nội dung học: {Items}", string.Join(", ", counts));
    }

    private int SeedAssignments(LearningSeedFile data)
    {
        var byId = new Dictionary<string, Assignment>();
        foreach (var a in data.Assignments)
        {
            var quiz = a.Quiz is { } q
                ? new QuizConfig(q.LessonIds.Select(Lesson).ToList(), q.Skills.Select(Enum<JapaneseSkill>).ToList(), Enum<QuizDifficulty>(q.Difficulty),
                    q.QuestionCount, q.TimeLimitMinutes, q.Attempts, q.Shuffle)
                : null;
            var due = new DateTimeOffset(DateOnly.Parse(a.DueDate).ToDateTime(new TimeOnly(23, 59)), Vn).ToUniversalTime();
            var created = Assignment.Create(Class(a.ClassId), Enum<AssignmentType>(a.Type), a.Title, a.LessonId is null ? null : Lesson(a.LessonId),
                a.Description, a.AssignedAt, due, a.MaxScore, quiz, (a.Attachments ?? []).Select(File).ToList(), Person(a.CreatedById) ?? Guid.Empty);
            if (Skip(created, $"bài tập {a.Id}")) continue;
            db.AddWithId(created.Value, SeedData.IdOf(a.Id));
            db.SetCreatedAt(created.Value, a.AssignedAt);
            byId[a.Id] = created.Value;
        }

        foreach (var s in data.Submissions)
        {
            if (!byId.TryGetValue(s.AssignmentId, out var a)) continue;
            Submission sub;
            if (s.Quiz is { } q && a.Quiz is { } cfg)
            {
                var first = Submission.SubmitQuiz(a.Id, SeedData.IdOf(s.StudentId), q.Correct, q.Total, q.DurationMinutes, a.MaxScore, cfg.Attempts,
                    s.SubmittedAt);
                if (Skip(first, $"bài nộp {s.Id}")) continue;
                sub = first.Value;
                // Các lần làm thêm (điểm thấp hơn — giữ lần cao nhất)
                for (var i = 1; i < q.Attempts; i++) sub.RecordQuizAttempt(Math.Max(0, q.Correct - i), q.Total, q.DurationMinutes, a.MaxScore, cfg.Attempts, s.SubmittedAt);
            }
            else
            {
                var files = Submission.SubmitFiles(a.Id, SeedData.IdOf(s.StudentId), s.Files.Select(File).ToList(), s.Content, s.SubmittedAt);
                if (Skip(files, $"bài nộp {s.Id}")) continue;
                sub = files.Value;
                if (s.Score is { } score && s.GradedAt is { } at
                    && Skip(sub.Grade(score, a.MaxScore, s.Comment, Person(s.GradedById) ?? a.CreatedByTeacherId, at), $"chấm {s.Id}")) continue;
            }
            db.AddWithId(sub, SeedData.IdOf(s.Id));
            db.SetCreatedAt(sub, s.SubmittedAt);
        }
        return byId.Count;
    }

    private int SeedExamResults(LearningSeedFile data)
    {
        var n = 0;
        foreach (var r in data.ExamResults)
        {
            var classId = Class(r.ClassId);
            var examId = new ExamMilestoneId(SeedData.IdOf(r.ExamId));
            ExamResult result;
            if (r.Status == "absent")
            {
                result = ExamResult.Absent(classId, examId, null, SeedData.IdOf(r.StudentId));
                result.UpdateComment(r.Comment);
            }
            else
            {
                var recorded = ExamResult.Record(classId, examId, null, SeedData.IdOf(r.StudentId),
                    r.Sections.Select(x => new ExamSectionScore(x.Name, x.Score, x.Max)).ToList(), r.StartedAt, r.DurationMinutes, r.Comment);
                if (Skip(recorded, $"kết quả thi {r.Id}")) continue;
                result = recorded.Value;
            }
            db.AddWithId(result, SeedData.IdOf(r.Id));
            n++;
        }
        return n;
    }

    private int SeedAnnouncements(LearningSeedFile data)
    {
        var n = 0;
        foreach (var a in data.Announcements)
        {
            var created = ClassAnnouncement.Create(Class(a.ClassId), a.Title, a.Content, a.Audience.Select(Enum<AnnouncementAudience>).ToList(),
                a.Channels.Select(Enum<AnnouncementChannel>).ToList(), a.DebtorsOnly ?? false, (a.StudentIds ?? []).Select(SeedData.IdOf).ToList(),
                Person(a.SentById) ?? Guid.Empty, null, a.SentAt);
            if (Skip(created, $"thông báo {a.Id}")) continue;
            var ann = created.Value;
            if (Skip(ann.MarkSent(a.Recipients, a.SentAt), $"gửi thông báo {a.Id}")) continue;
            for (var i = 0; i < Math.Min(a.ReadCount ?? 0, a.Recipients); i++) ann.MarkRead(SeedData.IdOf($"{a.Id}-reader-{i}"));
            db.AddWithId(ann, SeedData.IdOf(a.Id));
            db.SetCreatedAt(ann, a.SentAt);
            n++;
        }
        return n;
    }

    private int SeedReviews(LearningSeedFile data)
    {
        var n = 0;
        foreach (var r in data.TeacherReviews)
        {
            var created = TeacherReview.Create(Class(r.ClassId), SeedData.IdOf(r.TeacherId), SeedData.IdOf(r.StudentId), Enum<ReviewPeriod>(r.Period),
                r.Overall, r.Ratings.Method, r.Ratings.Clarity, r.Ratings.Enthusiasm, r.Ratings.Punctuality, r.Comment, r.Anonymous, r.CreatedAt);
            if (Skip(created, $"đánh giá {r.Id}")) continue;
            db.AddWithId(created.Value, SeedData.IdOf(r.Id));
            n++;
        }
        return n;
    }

    private int SeedDecks(LearningSeedFile data)
    {
        var decks = new Dictionary<string, FlashDeck>();
        foreach (var d in data.Decks)
        {
            var created = FlashDeck.Create(d.Title, Enum<JlptLevel>(d.Level), Enum<DeckKind>(d.Kind), d.LessonId is null ? null : Lesson(d.LessonId),
                d.Lesson);
            if (Skip(created, $"bộ thẻ {d.Id}")) continue;
            var deck = created.Value;
            foreach (var c in d.Cards)
            {
                var card = deck.AddCard(c.Front, c.Reading, c.Meaning, c.Example);
                if (Skip(card, $"thẻ {c.Id}")) continue;
                typeof(Entity<FlashCardId>).GetProperty(nameof(Entity<FlashCardId>.Id))!.SetValue(card.Value, new FlashCardId(SeedData.IdOf(c.Id)));
            }
            db.AddWithId(deck, SeedData.IdOf(d.Id));
            decks[d.Id] = deck;
        }

        foreach (var p in data.CardProgress)
        {
            if (p.DeckId is null || !decks.ContainsKey(p.DeckId)) continue;
            var due = DateOnly.FromDateTime(p.Due.ToOffset(Vn).DateTime);
            var progress = CardProgress.Import(SeedData.IdOf(p.StudentId), new FlashDeckId(SeedData.IdOf(p.DeckId)), new FlashCardId(SeedData.IdOf(p.CardId)),
                p.Box, due, p.Reviews, p.Lapses, p.Reviews > 0 ? p.Due.AddDays(-1) : null);
            db.AddWithId(progress, SeedData.IdOf(p.Id));
        }
        return decks.Count;
    }

    private bool Skip<T>(T result, string what) where T : Result
    {
        if (result.IsSuccess) return false;
        logger.LogWarning("Bỏ qua {What}: {Error}", what, result.Error.Message);
        return true;
    }

    private static AttachedFile File(FileSeed f) => new(f.Name, Enum<FileKind>(f.Kind), f.Url, f.Size);

    private static T Enum<T>(string value) where T : struct, global::System.Enum => SnakeCaseEnumConverter<T>.Parse(value);

    private static ClassId Class(string mockId) => new(SeedData.IdOf(mockId));

    private static LessonId Lesson(string mockId) => new(SeedData.IdOf(mockId));

    private static Guid? Person(string? mockId) => mockId is null ? null : SeedData.IdOf(mockId);

    // ---------- Định dạng learning.seed.json ----------

    private sealed record LearningSeedFile(List<AssignmentSeed> Assignments, List<SubmissionSeed> Submissions, List<ExamResultSeed> ExamResults,
        List<AnnouncementSeed> Announcements, List<ReviewSeed> TeacherReviews, List<DeckSeed> Decks, List<ProgressSeed> CardProgress);

    private sealed record FileSeed(string Name, string Kind, string Url, long? Size);

    private sealed record QuizSeed(List<string> LessonIds, List<string> Skills, string Difficulty, int QuestionCount, int? TimeLimitMinutes, int Attempts,
        bool Shuffle);

    private sealed record AssignmentSeed(string Id, string ClassId, string Title, string? LessonId, string Type, string? Description, QuizSeed? Quiz,
        DateTimeOffset AssignedAt, string DueDate, decimal MaxScore, List<FileSeed>? Attachments, string? CreatedById);

    private sealed record QuizResultSeed(int Correct, int Total, int Attempts, int DurationMinutes);

    private sealed record SubmissionSeed(string Id, string AssignmentId, string StudentId, DateTimeOffset SubmittedAt, List<FileSeed> Files,
        QuizResultSeed? Quiz, string? Content, decimal? Score, string? Comment, DateTimeOffset? GradedAt, string? GradedById);

    private sealed record SectionSeed(string Name, decimal Score, decimal Max);

    private sealed record ExamResultSeed(string Id, string ClassId, string ExamId, string StudentId, string Status, List<SectionSeed> Sections,
        DateTimeOffset? StartedAt, int? DurationMinutes, string? Comment);

    private sealed record AnnouncementSeed(string Id, string ClassId, string Title, string Content, List<string> Audience, List<string> Channels,
        bool? DebtorsOnly, List<string>? StudentIds, int Recipients, int? ReadCount, string? SentById, DateTimeOffset SentAt);

    private sealed record RatingsSeed(int Method, int Clarity, int Enthusiasm, int Punctuality);

    private sealed record ReviewSeed(string Id, string ClassId, string TeacherId, string StudentId, string Period, int Overall, RatingsSeed Ratings,
        string? Comment, bool Anonymous, DateTimeOffset CreatedAt);

    private sealed record CardSeed(string Id, string Front, string? Reading, string Meaning, string? Example);

    private sealed record DeckSeed(string Id, string Title, string Level, string Kind, string Lesson, string? LessonId, List<CardSeed> Cards);

    private sealed record ProgressSeed(string Id, string StudentId, string? DeckId, string CardId, int Box, DateTimeOffset Due, int Reviews, int Lapses);
}
