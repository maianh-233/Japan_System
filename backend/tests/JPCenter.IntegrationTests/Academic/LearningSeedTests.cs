using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.Modules.Academic.Domain.Flashcards;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using JPCenter.Modules.Library.Domain.TeacherDocuments;
using JPCenter.Modules.Library.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Academic;

/// <summary>Giai đoạn 20 phase 2 — dữ liệu mẫu Nội dung học (Academic) + tài liệu GV (Library) nạp đúng từ mock frontend. Chỉ xét bản ghi mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class LearningSeedTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonElement Seed = SeedData.Read<JsonElement>(typeof(AcademicDbContext).Assembly, "learning.seed.json");
    private static readonly JsonElement Library = SeedData.Read<JsonElement>(typeof(LibraryDbContext).Assembly, "library.seed.json");

    private static List<JsonElement> Items(JsonElement root, string name) => root.GetProperty(name).EnumerateArray().ToList();

    private static Guid Id(JsonElement e) => SeedData.IdOf(e.GetProperty("id").GetString()!);

    [Fact]
    public async Task Bai_tap_bai_nop_va_ket_qua_thi()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();

        var mockAssignments = Items(Seed, "assignments");
        var ids = mockAssignments.Select(a => new AssignmentId(Id(a))).ToList();
        var assignments = await db.Assignments.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(Ct);
        assignments.Count.ShouldBe(mockAssignments.Count);
        var quiz = assignments.First(a => a.Type == AssignmentType.Quiz);
        quiz.Quiz!.LessonIds.ShouldNotBeEmpty();
        quiz.Quiz.Skills.ShouldContain(JapaneseSkill.Grammar);
        assignments.Where(a => a.Type == AssignmentType.Upload).ShouldAllBe(a => a.Quiz == null);

        var mockSubs = Items(Seed, "submissions");
        var subIds = mockSubs.Select(s => new SubmissionId(Id(s))).ToList();
        var subs = await db.Submissions.AsNoTracking().Where(s => subIds.Contains(s.Id)).ToListAsync(Ct);
        subs.Count.ShouldBe(mockSubs.Count);
        // Quiz: tự chấm (không người chấm); bài file đã chấm có người chấm là GV của lớp
        var quizIds = assignments.Where(a => a.Type == AssignmentType.Quiz).Select(a => a.Id).ToHashSet();
        subs.Where(s => quizIds.Contains(s.AssignmentId)).ShouldAllBe(s => s.GradedBy == null && s.QuizCorrect != null && s.Score != null);
        subs.Where(s => !quizIds.Contains(s.AssignmentId) && s.Score != null).ShouldAllBe(s => s.GradedBy != null && s.Files.Count > 0);

        var mockResults = Items(Seed, "examResults");
        var resultIds = mockResults.Select(r => new ExamResultId(Id(r))).ToList();
        var results = await db.ExamResults.AsNoTracking().Where(r => resultIds.Contains(r.Id)).ToListAsync(Ct);
        results.Count.ShouldBe(mockResults.Count);
        results.Where(r => r.Status == ExamAttendance.Done).ShouldAllBe(r => r.MaxScore == 100 && r.Sections.Count == 3 && r.ClassExamId == null);
        results.Count(r => r.Status == ExamAttendance.Absent).ShouldBe(mockResults.Count(r => r.GetProperty("status").GetString() == "absent"));
    }

    [Fact]
    public async Task Thong_bao_danh_gia_GV_flashcard_va_tai_lieu_GV()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();

        var mockAnn = Items(Seed, "announcements");
        var annIds = mockAnn.Select(a => new ClassAnnouncementId(Id(a))).ToList();
        var anns = await db.ClassAnnouncements.AsNoTracking().Where(a => annIds.Contains(a.Id)).ToListAsync(Ct);
        // Thông báo mẫu 0 người nhận (lớp không còn HV đang học) bị bỏ — ND-07.E1
        anns.Count.ShouldBe(mockAnn.Count(a => a.GetProperty("recipients").GetInt32() > 0));
        anns.ShouldAllBe(a => a.SentAt != null && a.Recipients > 0 && a.ReadCount == a.ReadBy.Count && a.ReadCount <= a.Recipients);
        anns.ShouldContain(a => a.DebtorsOnly && a.Channels.Contains(AnnouncementChannel.Email));

        var mockReviews = Items(Seed, "teacherReviews");
        var reviewIds = mockReviews.Select(r => new TeacherReviewId(Id(r))).ToList();
        (await db.TeacherReviews.AsNoTracking().CountAsync(r => reviewIds.Contains(r.Id), Ct)).ShouldBe(mockReviews.Count);

        var mockDecks = Items(Seed, "decks");
        var deckIds = mockDecks.Select(d => new FlashDeckId(Id(d))).ToList();
        var decks = await db.FlashDecks.AsNoTracking().Where(d => deckIds.Contains(d.Id)).ToListAsync(Ct);
        decks.Count.ShouldBe(mockDecks.Count);
        decks.Sum(d => d.Cards.Count).ShouldBe(mockDecks.Sum(d => d.GetProperty("cards").GetArrayLength()));
        decks.Single(d => d.Id == new FlashDeckId(SeedData.IdOf("D-N3-L10"))).LessonId.ShouldNotBeNull();
        decks.SelectMany(d => d.Cards).ShouldContain(c => c.Id == new FlashCardId(SeedData.IdOf("D-N3-L10-1")) && c.Front == "締め切り");

        var progressIds = Items(Seed, "cardProgress").Select(p => new CardProgressId(Id(p))).ToList();
        var progress = await db.CardProgress.AsNoTracking().Where(p => progressIds.Contains(p.Id)).ToListAsync(Ct);
        progress.Count.ShouldBe(progressIds.Count);
        progress.ShouldAllBe(p => p.StudentId == SeedData.IdOf("ST-1") && p.Box >= 0 && p.Box <= 5);

        var lib = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var mockDocs = Items(Library, "teacherDocs");
        var docIds = mockDocs.Select(d => new TeacherDocumentId(Id(d))).ToList();
        var docs = await lib.TeacherDocuments.AsNoTracking().Where(d => docIds.Contains(d.Id)).ToListAsync(Ct);
        docs.Count.ShouldBe(mockDocs.Count);
        var first = docs.Single(d => d.Id == new TeacherDocumentId(SeedData.IdOf("TD-1")));
        (first.Kind, first.Views.Count).ShouldBe((TeacherDocKind.Pdf, 3));
        first.ClassIds.ShouldBe([SeedData.IdOf("C-N3K12")]);
        docs.Single(d => d.Kind == TeacherDocKind.Link).Url.ShouldStartWith("https://");
    }
}
