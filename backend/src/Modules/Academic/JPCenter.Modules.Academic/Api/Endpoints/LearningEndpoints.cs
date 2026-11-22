using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Api.Common.Validation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Announcements.CancelAnnouncement;
using JPCenter.Modules.Academic.Application.Announcements.GetClassAnnouncements;
using JPCenter.Modules.Academic.Application.Announcements.GetMyAnnouncements;
using JPCenter.Modules.Academic.Application.Announcements.MarkAnnouncementRead;
using JPCenter.Modules.Academic.Application.Announcements.SendAnnouncement;
using JPCenter.Modules.Academic.Application.Assignments.Common;
using JPCenter.Modules.Academic.Application.Assignments.CreateAssignment;
using JPCenter.Modules.Academic.Application.Assignments.DeleteAssignment;
using JPCenter.Modules.Academic.Application.Assignments.GetClassAssignments;
using JPCenter.Modules.Academic.Application.Assignments.GetFileLink;
using JPCenter.Modules.Academic.Application.Assignments.GetMyAssignments;
using JPCenter.Modules.Academic.Application.Assignments.GradeSubmission;
using JPCenter.Modules.Academic.Application.Assignments.StartQuiz;
using JPCenter.Modules.Academic.Application.Assignments.SubmitAssignment;
using JPCenter.Modules.Academic.Application.Assignments.SubmitQuiz;
using JPCenter.Modules.Academic.Application.Assignments.UpdateAssignment;
using JPCenter.Modules.Academic.Application.Assignments.UploadLearningFile;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Application.Exams.ChangeExamStatus;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Application.Exams.CreateExam;
using JPCenter.Modules.Academic.Application.Exams.DrawExamQuestions;
using JPCenter.Modules.Academic.Application.Exams.GetClassExams;
using JPCenter.Modules.Academic.Application.Exams.GetExamPaper;
using JPCenter.Modules.Academic.Application.Exams.GetExamResults;
using JPCenter.Modules.Academic.Application.Exams.GetExamReview;
using JPCenter.Modules.Academic.Application.Exams.GetMyExams;
using JPCenter.Modules.Academic.Application.Exams.SaveExamAnswers;
using JPCenter.Modules.Academic.Application.Exams.SaveExamResult;
using JPCenter.Modules.Academic.Application.Exams.StartExam;
using JPCenter.Modules.Academic.Application.Exams.SubmitExam;
using JPCenter.Modules.Academic.Application.Exams.UpdateExam;
using JPCenter.Modules.Academic.Application.Flashcards.Common;
using JPCenter.Modules.Academic.Application.Flashcards.DeleteFlashDeck;
using JPCenter.Modules.Academic.Application.Flashcards.EditFlashCards;
using JPCenter.Modules.Academic.Application.Flashcards.GetFlashDecks;
using JPCenter.Modules.Academic.Application.Flashcards.MyFlashcards;
using JPCenter.Modules.Academic.Application.Flashcards.SaveFlashDeck;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Application.TeacherReviews.Common;
using JPCenter.Modules.Academic.Application.TeacherReviews.GetMySurveys;
using JPCenter.Modules.Academic.Application.TeacherReviews.GetTeacherReviews;
using JPCenter.Modules.Academic.Application.TeacherReviews.SubmitTeacherReview;
using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.Modules.Academic.Domain.Flashcards;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>
/// Nội dung học (user story 12, ND-01 → ND-11): bài tập, kỳ thi, thông báo lớp, đánh giá GV, flashcard.
/// Nhân viên: quyền trang Lớp học <c>academic.classes</c> (flashcard: <c>academic.curriculum</c>) · cổng GV <c>/portal/teaching</c>: lớp mình dạy ·
/// cổng HV / PH <c>/portal/learning</c>: theo chủ tài khoản (PH chỉ xem; làm / nộp bài, đánh giá GV, ôn thẻ: chỉ HV).
/// </summary>
public static class LearningEndpoints
{
    /// <summary>Tệp bài tập / bài nộp ≤ 20 MB + phần form</summary>
    public const long UploadLimit = 21 * 1024 * 1024;

    private static readonly Error FileRequired = Error.Validation("file.required", "Chọn tệp cần tải lên (form-data, trường \"file\")");

    public sealed record GradeBody(decimal Score, string? Comment);

    public sealed record SubmitBody(List<SubmissionFileDto>? Files, string? Content);

    public sealed record QuizSubmitBody(string Token, List<QuizAnswerInput>? Answers);

    public sealed record DrawBody(Guid ExamId, List<ExamSectionInput> Sections, List<Guid>? Exclude);

    public sealed record ResultBody(bool Absent, List<decimal>? Scores, string? Comment);

    public sealed record AnswersBody(List<ExamAnswerInput> Answers);

    public sealed record AnnouncementBody(string? Title, string? Content, List<AnnouncementAudience>? Audience, List<AnnouncementChannel>? Channels,
        bool DebtorsOnly, List<Guid>? StudentIds, DateTimeOffset? ScheduledAt);

    public sealed record ReviewBody(Guid ClassId, Guid TeacherId, int Overall, ReviewRatingsDto Ratings, string? Comment, bool Anonymous);

    public sealed record DeckBody(string? Title, JlptLevel Level, DeckKind Kind, Guid? LessonId, string? LessonLabel, List<FlashCardInput>? Cards);

    public sealed record CardsBody(List<FlashCardInput> Cards);

    public sealed record OrderBody(List<Guid> CardIds);

    public sealed record CardReviewsBody(List<CardReviewInput> Reviews);

    public static void Map(IEndpointRouteBuilder academic)
    {
        MapStaff(academic);
        MapTeaching(academic.MapGroup("/portal/teaching").RequirePortal(Portal.Teacher));
        MapLearning(academic.MapGroup("/portal/learning").RequirePortal(Portal.Student, Portal.Parent));
    }

    private static void MapStaff(IEndpointRouteBuilder g)
    {
        const string page = AcademicAccess.Classes;
        const PermissionAction view = PermissionAction.View, update = PermissionAction.Update;

        // ---------- Bài tập (ND-01, 03) ----------
        g.MapGet("/classes/{id:guid}/assignments", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetClassAssignmentsQuery(id, false), ct)).ToHttpResult()).RequirePermission(page, view);
        g.MapPost("/classes/{id:guid}/assignments", async (Guid id, AssignmentInput b, ISender s, CancellationToken ct) =>
            (await s.Send(new CreateAssignmentCommand(id, false, b), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapPut("/assignments/{id:guid}", async (Guid id, AssignmentInput b, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateAssignmentCommand(id, false, b), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapDelete("/assignments/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new DeleteAssignmentCommand(id, false), ct)).ToHttpResult()).RequirePermission(page, update);
        MapUpload(g, "/assignments/files").RequirePermission(page, update);
        MapFileLinks(g, LearningViewer.Staff).RequirePermission(page, view);
        g.MapPut("/submissions/{id:guid}/grade", async (Guid id, GradeBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new GradeSubmissionCommand(id, false, b.Score, b.Comment), ct)).ToHttpResult()).RequirePermission(page, update);

        // ---------- Kỳ thi (ND-04, 06) ----------
        g.MapGet("/classes/{id:guid}/exams", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetClassExamsQuery(id, false), ct)).ToHttpResult()).RequirePermission(page, view);
        g.MapPost("/classes/{id:guid}/exams/draw", async (Guid id, DrawBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new DrawExamQuestionsCommand(id, b.ExamId, b.Sections, b.Exclude), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapPost("/classes/{id:guid}/exams", async (Guid id, ExamInput b, ISender s, CancellationToken ct) =>
            (await s.Send(new CreateExamCommand(id, b), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapPut("/exams/{id:guid}", async (Guid id, ExamInput b, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateExamCommand(id, b), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapPost("/exams/{id:guid}/publish", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new ChangeExamStatusCommand(id, ExamAction.Publish), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapPost("/exams/{id:guid}/cancel", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new ChangeExamStatusCommand(id, ExamAction.Cancel), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapPost("/exams/{id:guid}/publish-results", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new ChangeExamStatusCommand(id, ExamAction.PublishResults), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapGet("/exams/{id:guid}/paper", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetExamPaperQuery(id, false), ct)).ToHttpResult()).RequirePermission(page, view);
        g.MapGet("/exams/{id:guid}/results", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetExamResultsQuery(id, false), ct)).ToHttpResult()).RequirePermission(page, view);
        g.MapPut("/exams/{id:guid}/results/{studentId:guid}", async (Guid id, Guid studentId, ResultBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SaveExamResultCommand(id, studentId, false, b.Absent, b.Scores, b.Comment), ct)).ToHttpResult())
            .RequirePermission(page, update);

        // ---------- Thông báo lớp (ND-07), đánh giá GV (ND-08) ----------
        g.MapGet("/classes/{id:guid}/announcements", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetClassAnnouncementsQuery(id, false), ct)).ToHttpResult()).RequirePermission(page, view);
        g.MapPost("/classes/{id:guid}/announcements", async (Guid id, AnnouncementBody b, ISender s, CancellationToken ct) =>
            (await s.Send(Send(id, false, b), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapDelete("/announcements/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new CancelAnnouncementCommand(id, false), ct)).ToHttpResult()).RequirePermission(page, update);
        g.MapGet("/classes/{id:guid}/teacher-reviews", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetTeacherReviewsQuery(id, false), ct)).ToHttpResult()).RequirePermission(page, view);

        // ---------- Flashcard (ND-09) — trang Lộ trình ----------
        const string curriculum = CurriculumReader.Page;
        var decks = g.MapGroup("/flash-decks");
        decks.MapGet("/", async (Guid? curriculumId, Guid? lessonId, JlptLevel? level, ISender s, CancellationToken ct) =>
            (await s.Send(new GetFlashDecksQuery(curriculumId, lessonId, level), ct)).ToHttpResult()).RequirePermission(curriculum, view);
        decks.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetFlashDeckQuery(id), ct)).ToHttpResult()).RequirePermission(curriculum, view);
        decks.MapPost("/", async (DeckBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SaveFlashDeckCommand(null, b.Title, b.Level, b.Kind, b.LessonId, b.LessonLabel, b.Cards), ct)).ToHttpResult())
            .RequirePermission(curriculum, update);
        decks.MapPut("/{id:guid}", async (Guid id, DeckBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SaveFlashDeckCommand(id, b.Title, b.Level, b.Kind, b.LessonId, b.LessonLabel, null), ct)).ToHttpResult())
            .RequirePermission(curriculum, update);
        decks.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new DeleteFlashDeckCommand(id), ct)).ToHttpResult()).RequirePermission(curriculum, update);
        decks.MapPost("/{id:guid}/cards", async (Guid id, CardsBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new AddFlashCardsCommand(id, b.Cards), ct)).ToHttpResult()).RequirePermission(curriculum, update);
        decks.MapPut("/{id:guid}/cards/order", async (Guid id, OrderBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new ReorderFlashCardsCommand(id, b.CardIds), ct)).ToHttpResult()).RequirePermission(curriculum, update);
        decks.MapPut("/{id:guid}/cards/{cardId:guid}", async (Guid id, Guid cardId, FlashCardInput b, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateFlashCardCommand(id, cardId, b), ct)).ToHttpResult()).RequirePermission(curriculum, update);
        decks.MapDelete("/{id:guid}/cards/{cardId:guid}", async (Guid id, Guid cardId, ISender s, CancellationToken ct) =>
            (await s.Send(new RemoveFlashCardCommand(id, cardId), ct)).ToHttpResult()).RequirePermission(curriculum, update);
    }

    /// <summary>Cổng GV — bài tập, kỳ thi (xem đề, nhập điểm, công bố), thông báo lớp, đánh giá về mình</summary>
    private static void MapTeaching(RouteGroupBuilder g)
    {
        g.MapGet("/classes/{id:guid}/assignments", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetClassAssignmentsQuery(id, true), ct)).ToHttpResult());
        g.MapPost("/classes/{id:guid}/assignments", async (Guid id, AssignmentInput b, ISender s, CancellationToken ct) =>
            (await s.Send(new CreateAssignmentCommand(id, true, b), ct)).ToHttpResult());
        g.MapPut("/assignments/{id:guid}", async (Guid id, AssignmentInput b, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateAssignmentCommand(id, true, b), ct)).ToHttpResult());
        g.MapDelete("/assignments/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new DeleteAssignmentCommand(id, true), ct)).ToHttpResult());
        MapUpload(g, "/files");
        MapFileLinks(g, LearningViewer.Teacher);
        g.MapPut("/submissions/{id:guid}/grade", async (Guid id, GradeBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new GradeSubmissionCommand(id, true, b.Score, b.Comment), ct)).ToHttpResult());

        g.MapGet("/classes/{id:guid}/exams", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetClassExamsQuery(id, true), ct)).ToHttpResult());
        g.MapGet("/exams/{id:guid}/paper", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetExamPaperQuery(id, true), ct)).ToHttpResult());
        g.MapGet("/exams/{id:guid}/results", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetExamResultsQuery(id, true), ct)).ToHttpResult());
        g.MapPut("/exams/{id:guid}/results/{studentId:guid}", async (Guid id, Guid studentId, ResultBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SaveExamResultCommand(id, studentId, true, b.Absent, b.Scores, b.Comment), ct)).ToHttpResult());
        g.MapPost("/exams/{id:guid}/publish-results", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new ChangeExamStatusCommand(id, ExamAction.PublishResults, AsTeacher: true), ct)).ToHttpResult());

        g.MapGet("/classes/{id:guid}/announcements", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetClassAnnouncementsQuery(id, true), ct)).ToHttpResult());
        g.MapPost("/classes/{id:guid}/announcements", async (Guid id, AnnouncementBody b, ISender s, CancellationToken ct) =>
            (await s.Send(Send(id, true, b), ct)).ToHttpResult());
        g.MapDelete("/announcements/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new CancelAnnouncementCommand(id, true), ct)).ToHttpResult());

        g.MapGet("/reviews", async (ISender s, CancellationToken ct) => (await s.Send(new GetTeacherReviewsQuery(null, true), ct)).ToHttpResult());
    }

    /// <summary>Cổng HV / PH — ND-02, 05, 07, 08, 09, 11</summary>
    private static void MapLearning(RouteGroupBuilder g)
    {
        // Bài tập
        g.MapGet("/assignments", async (Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetMyAssignmentsQuery(studentId), ct)).ToHttpResult());
        MapUpload(g, "/files").RequirePortal(Portal.Student);
        MapFileLinks(g, LearningViewer.Learner);
        g.MapPost("/assignments/{id:guid}/submissions", async (Guid id, SubmitBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SubmitAssignmentCommand(id, b.Files, b.Content), ct)).ToHttpResult()).RequirePortal(Portal.Student);
        g.MapPost("/assignments/{id:guid}/quiz/start", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new StartQuizCommand(id), ct)).ToHttpResult()).RequirePortal(Portal.Student);
        g.MapPost("/assignments/{id:guid}/quiz/submit", async (Guid id, QuizSubmitBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SubmitQuizCommand(id, b.Token, b.Answers), ct)).ToHttpResult()).RequirePortal(Portal.Student);

        // Kỳ thi
        g.MapGet("/exams", async (Guid? studentId, ISender s, CancellationToken ct) => (await s.Send(new GetMyExamsQuery(studentId), ct)).ToHttpResult());
        g.MapGet("/exams/{id:guid}/review", async (Guid id, Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetExamReviewQuery(id, studentId), ct)).ToHttpResult());
        g.MapPost("/exams/{id:guid}/start", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new StartExamCommand(id), ct)).ToHttpResult()).RequirePortal(Portal.Student);
        g.MapPut("/exam-attempts/{id:guid}/answers", async (Guid id, AnswersBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SaveExamAnswersCommand(id, b.Answers), ct)).ToHttpResult()).RequirePortal(Portal.Student);
        g.MapPost("/exam-attempts/{id:guid}/submit", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new SubmitExamCommand(id), ct)).ToHttpResult()).RequirePortal(Portal.Student);

        // Thông báo lớp
        g.MapGet("/announcements", async (Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetMyAnnouncementsQuery(studentId), ct)).ToHttpResult());
        g.MapPost("/announcements/{id:guid}/read", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new MarkAnnouncementReadCommand(id), ct)).ToHttpResult());

        // Đánh giá GV (chỉ HV)
        g.MapGet("/teacher-reviews/surveys", async (ISender s, CancellationToken ct) =>
            (await s.Send(new GetMySurveysQuery(), ct)).ToHttpResult()).RequirePortal(Portal.Student);
        g.MapPost("/teacher-reviews", async (ReviewBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new SubmitTeacherReviewCommand(b.ClassId, b.TeacherId, b.Overall, b.Ratings, b.Comment, b.Anonymous), ct)).ToHttpResult())
            .RequirePortal(Portal.Student);

        // Flashcard
        g.MapGet("/flashcards", async (Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetMyFlashcardsQuery(studentId), ct)).ToHttpResult());
        g.MapPost("/flashcards/reviews", async (CardReviewsBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new ReviewFlashcardsCommand(b.Reviews), ct)).ToHttpResult()).RequirePortal(Portal.Student);
    }

    private static SendAnnouncementCommand Send(Guid classId, bool asTeacher, AnnouncementBody b) =>
        new(classId, asTeacher, b.Title, b.Content, b.Audience, b.Channels, b.DebtorsOnly, b.StudentIds, b.ScheduledAt);

    /// <summary>Tải 1 tệp (multipart, trường "file") → <c>SubmissionFile</c> để gửi kèm khi giao / nộp bài</summary>
    private static RouteHandlerBuilder MapUpload(IEndpointRouteBuilder g, string path) =>
        g.MapPost(path, async (HttpRequest request, ISender s, CancellationToken ct) =>
            {
                if (!request.HasFormContentType) return FileRequired.ToProblem();
                var form = await request.ReadFormAsync(ct);
                if (form.Files.GetFile("file") is not { } file) return FileRequired.ToProblem();
                await using var stream = file.OpenReadStream();
                return (await s.Send(new UploadLearningFileCommand(stream, file.FileName), ct)).ToHttpResult();
            })
            .WithBodyLimit(UploadLimit)
            .DisableAntiforgery();

    /// <summary>Link tạm đề đính kèm / tệp bài nộp (kiểm quyền xem theo người mở)</summary>
    private static RouteGroupBuilder MapFileLinks(IEndpointRouteBuilder g, LearningViewer viewer)
    {
        var links = g.MapGroup("/assignments/{id:guid}");
        links.MapGet("/attachments/{index:int}/link", async (Guid id, int index, Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetAssignmentFileLinkQuery(id, null, index, viewer, studentId), ct)).ToHttpResult());
        links.MapGet("/submissions/{submissionId:guid}/files/{index:int}/link",
            async (Guid id, Guid submissionId, int index, Guid? studentId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetAssignmentFileLinkQuery(id, submissionId, index, viewer, studentId), ct)).ToHttpResult());
        return links;
    }
}
