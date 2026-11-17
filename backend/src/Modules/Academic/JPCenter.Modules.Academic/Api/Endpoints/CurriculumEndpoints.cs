using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Api.Common.Validation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Curricula.AddChapter;
using JPCenter.Modules.Academic.Application.Curricula.AddExam;
using JPCenter.Modules.Academic.Application.Curricula.AddLesson;
using JPCenter.Modules.Academic.Application.Curricula.AddLinkMaterial;
using JPCenter.Modules.Academic.Application.Curricula.ArchiveCurriculum;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Application.Curricula.CreateCurriculum;
using JPCenter.Modules.Academic.Application.Curricula.DeleteCurriculum;
using JPCenter.Modules.Academic.Application.Curricula.GetCurricula;
using JPCenter.Modules.Academic.Application.Curricula.GetCurriculum;
using JPCenter.Modules.Academic.Application.Curricula.GetMaterialLink;
using JPCenter.Modules.Academic.Application.Curricula.NewCurriculumVersion;
using JPCenter.Modules.Academic.Application.Curricula.PublishCurriculum;
using JPCenter.Modules.Academic.Application.Curricula.RemoveChapter;
using JPCenter.Modules.Academic.Application.Curricula.RemoveExam;
using JPCenter.Modules.Academic.Application.Curricula.RemoveLesson;
using JPCenter.Modules.Academic.Application.Curricula.RemoveMaterial;
using JPCenter.Modules.Academic.Application.Curricula.ReorderChapters;
using JPCenter.Modules.Academic.Application.Curricula.ReorderLessons;
using JPCenter.Modules.Academic.Application.Curricula.RestoreCurriculum;
using JPCenter.Modules.Academic.Application.Curricula.UpdateChapter;
using JPCenter.Modules.Academic.Application.Curricula.UpdateCurriculum;
using JPCenter.Modules.Academic.Application.Curricula.UpdateExam;
using JPCenter.Modules.Academic.Application.Curricula.UpdateLesson;
using JPCenter.Modules.Academic.Application.Curricula.UpdateMaterial;
using JPCenter.Modules.Academic.Application.Curricula.UploadMaterial;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>
/// LT-01 → LT-11 — <c>/api/v1/academic/curricula</c>, quyền <c>academic.curriculum.*</c>.
/// Mọi thao tác soạn nội dung trả về lộ trình đầy đủ mới nhất (frontend thay nguyên object).
/// </summary>
public static class CurriculumEndpoints
{
    /// <summary>Tệp ≤ 50 MB + phần form khác</summary>
    public const long UploadLimit = 51 * 1024 * 1024;

    private static readonly Error FileRequired = Error.Validation("file.required", "Chọn tệp cần tải lên (form-data, trường \"file\")");

    public sealed record CurriculumBody(string Code, string Name, JlptLevel Level, CurriculumTier Tier, string? EntryRequirement, string Version,
        string? Textbook, string? Description, List<string>? Tags, Guid? CloneFromId, uint? RowVersion)
    {
        public CurriculumInput ToInput() => new(Code, Name, Level, Tier, EntryRequirement, Version, Textbook, Description, Tags);
    }

    public sealed record PublishBody(List<Guid>? SetDefaultFor);
    public sealed record LessonBody(string Title, bool HasQuiz);
    public sealed record TitleBody(string Title);
    public sealed record OrderBody(List<Guid> Ids);
    public sealed record LinkMaterialBody(string Name, MaterialType Type, string Url, bool SharedWithStudents);
    public sealed record MaterialBody(string Name, bool SharedWithStudents);
    public sealed record ExamBody(ExamKind Kind, string Title, Guid AfterLessonId, string? Note);

    public static void Map(IEndpointRouteBuilder academic)
    {
        var g = academic.MapGroup("/curricula");
        const string page = CurriculumReader.Page;

        // ---------- Danh sách, thông tin, vòng đời ----------
        g.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetCurriculaQuery(), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetCurriculumQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        g.MapPost("/", async (CurriculumBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateCurriculumCommand(b.ToInput(), b.CloneFromId), ct))
                .ToCreatedResult(c => $"/api/v1/academic/curricula/{c.Id}"))
            .RequirePermission(page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, CurriculumBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateCurriculumCommand(id, b.ToInput(), b.RowVersion ?? 0), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/publish", async (Guid id, PublishBody? b, ISender s, CancellationToken ct) =>
                (await s.Send(new PublishCurriculumCommand(id, b?.SetDefaultFor), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/new-version", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new NewCurriculumVersionCommand(id), ct)).ToCreatedResult(c => $"/api/v1/academic/curricula/{c.Id}"))
            .RequirePermission(page, PermissionAction.Create);

        g.MapPost("/{id:guid}/archive", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new ArchiveCurriculumCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/restore", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new RestoreCurriculumCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new DeleteCurriculumCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Delete);

        // ---------- Bài học ----------
        g.MapPost("/{id:guid}/lessons", async (Guid id, LessonBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AddLessonCommand(id, b.Title, b.HasQuiz), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPut("/{id:guid}/lessons/order", async (Guid id, OrderBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ReorderLessonsCommand(id, b.Ids), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPut("/{id:guid}/lessons/{lessonId:guid}", async (Guid id, Guid lessonId, LessonBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateLessonCommand(id, lessonId, b.Title, b.HasQuiz), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapDelete("/{id:guid}/lessons/{lessonId:guid}", async (Guid id, Guid lessonId, ISender s, CancellationToken ct) =>
                (await s.Send(new RemoveLessonCommand(id, lessonId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // ---------- Chapter ----------
        var chapters = g.MapGroup("/{id:guid}/lessons/{lessonId:guid}/chapters");
        chapters.MapPost("/", async (Guid id, Guid lessonId, TitleBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AddChapterCommand(id, lessonId, b.Title), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        chapters.MapPut("/order", async (Guid id, Guid lessonId, OrderBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ReorderChaptersCommand(id, lessonId, b.Ids), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        chapters.MapPut("/{chapterId:guid}", async (Guid id, Guid lessonId, Guid chapterId, TitleBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateChapterCommand(id, lessonId, chapterId, b.Title), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        chapters.MapDelete("/{chapterId:guid}", async (Guid id, Guid lessonId, Guid chapterId, ISender s, CancellationToken ct) =>
                (await s.Send(new RemoveChapterCommand(id, lessonId, chapterId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // ---------- Tài liệu ----------
        // LT-06: tải 1 tệp / request (multipart: file, name?, sharedWithStudents?) — giới hạn riêng 51 MB. Đọc form TRONG handler để
        // kiểm quyền trước khi nhận body (người không có quyền → 403, không bắt server đọc file). JWT ở header nên không cần antiforgery.
        chapters.MapPost("/{chapterId:guid}/materials/file", async (Guid id, Guid lessonId, Guid chapterId, HttpRequest request, ISender s,
                CancellationToken ct) =>
            {
                if (!request.HasFormContentType) return FileRequired.ToProblem();
                var form = await request.ReadFormAsync(ct);
                if (form.Files.GetFile("file") is not { } file) return FileRequired.ToProblem();
                var shared = !bool.TryParse(form["sharedWithStudents"], out var v) || v;
                await using var stream = file.OpenReadStream();
                return (await s.Send(new UploadMaterialCommand(id, lessonId, chapterId, stream, file.FileName, form["name"].FirstOrDefault(), shared), ct))
                    .ToHttpResult();
            })
            .RequirePermission(page, PermissionAction.Update)
            .WithBodyLimit(UploadLimit)
            .DisableAntiforgery();

        chapters.MapPost("/{chapterId:guid}/materials/link", async (Guid id, Guid lessonId, Guid chapterId, LinkMaterialBody b, ISender s,
                CancellationToken ct) =>
                (await s.Send(new AddLinkMaterialCommand(id, lessonId, chapterId, b.Name, b.Type, b.Url, b.SharedWithStudents), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPut("/{id:guid}/materials/{materialId:guid}", async (Guid id, Guid materialId, MaterialBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateMaterialCommand(id, materialId, b.Name, b.SharedWithStudents), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapDelete("/{id:guid}/materials/{materialId:guid}", async (Guid id, Guid materialId, ISender s, CancellationToken ct) =>
                (await s.Send(new RemoveMaterialCommand(id, materialId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // LT-07: link tạm 15 phút (download=true → tải về đúng tên gốc, mặc định mở xem)
        g.MapGet("/{id:guid}/materials/{materialId:guid}/link", async (Guid id, Guid materialId, bool? download, ISender s, CancellationToken ct) =>
                (await s.Send(new GetMaterialLinkQuery(id, materialId, download ?? false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // ---------- Mốc thi ----------
        g.MapPost("/{id:guid}/exams", async (Guid id, ExamBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new AddExamCommand(id, b.Kind, b.Title, b.AfterLessonId, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPut("/{id:guid}/exams/{examId:guid}", async (Guid id, Guid examId, ExamBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateExamCommand(id, examId, b.Kind, b.Title, b.AfterLessonId, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapDelete("/{id:guid}/exams/{examId:guid}", async (Guid id, Guid examId, ISender s, CancellationToken ct) =>
                (await s.Send(new RemoveExamCommand(id, examId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);
    }
}
