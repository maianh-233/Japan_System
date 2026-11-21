using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Api.Common.Validation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Questions.ChangeQuestionStatus;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Application.Questions.CreateQuestion;
using JPCenter.Modules.Academic.Application.Questions.DuplicateQuestion;
using JPCenter.Modules.Academic.Application.Questions.GetAudioLink;
using JPCenter.Modules.Academic.Application.Questions.GetQuestion;
using JPCenter.Modules.Academic.Application.Questions.GetQuestions;
using JPCenter.Modules.Academic.Application.Questions.GetQuestionStats;
using JPCenter.Modules.Academic.Application.Questions.ImportQuestions;
using JPCenter.Modules.Academic.Application.Questions.UpdateQuestion;
using JPCenter.Modules.Academic.Application.Questions.UploadAudio;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>
/// NH-01 → NH-08 — Ngân hàng câu hỏi (<c>/api/v1/academic/questions</c>). Quyền: Đào tạo › Ngân hàng câu hỏi
/// (<c>approve</c> = duyệt / trả lại / ngừng dùng / mở lại). Chỉ cổng nhân viên — có đáp án.
/// </summary>
public static class QuestionEndpoints
{
    private const string Page = QuestionBank.Page;
    public const long AudioLimit = 21 * 1024 * 1024;
    public const long ImportLimit = 5 * 1024 * 1024;

    private static readonly Error FileRequired = Error.Validation("file.required", "Chọn file nghe (form-data, trường \"file\")");

    /// <summary>Thêm / sửa: nội dung câu + gửi duyệt luôn (thêm) / rowVersion (sửa)</summary>
    public sealed record QuestionBody(JlptLevel Level, JapaneseSkill Skill, QuestionFormat Format, QuestionDifficulty Difficulty, string Stem,
        string? Passage, string? Transcript, List<string>? Options, int Answer, string? Explanation, Guid? CurriculumId, List<Guid>? LessonIds,
        List<string>? Tags, bool Submit = false, uint RowVersion = 0)
    {
        public QuestionInput ToInput() => new(Level, Skill, Format, Difficulty, Stem, Passage, Transcript, Options, Answer, Explanation, CurriculumId,
            LessonIds, Tags);
    }

    public sealed record IdsBody(List<Guid> Ids, string? Note);

    public sealed record ImportRowBody(int Row, JlptLevel Level, JapaneseSkill Skill, QuestionFormat Format, QuestionDifficulty Difficulty, string Stem,
        string? Passage, string? Transcript, List<string>? Options, int Answer, string? Explanation, Guid? CurriculumId, List<Guid>? LessonIds,
        List<string>? Tags);

    public sealed record ImportBody(List<ImportRowBody> Rows, bool Submit);

    /// <summary>Bộ lọc danh sách: <c>?level=N3&amp;skill=grammar&amp;status=review&amp;q=…</c></summary>
    public sealed record FilterQuery(QueryEnum<JlptLevel>? Level, QueryEnum<JapaneseSkill>? Skill, QueryEnum<QuestionFormat>? Format,
        QueryEnum<QuestionDifficulty>? Difficulty, QueryEnum<QuestionStatus>? Status, Guid? CurriculumId, Guid? LessonId, string? Tag, Guid? AuthorId,
        string? Q);

    public static void Map(IEndpointRouteBuilder academic)
    {
        var g = academic.MapGroup("/questions");

        g.MapGet("/", async ([AsParameters] FilterQuery f, ISender s, CancellationToken ct) =>
                (await s.Send(new GetQuestionsQuery(f.Level?.Value, f.Skill?.Value, f.Format?.Value, f.Difficulty?.Value, f.Status?.Value, f.CurriculumId,
                    f.LessonId, f.Tag, f.AuthorId, f.Q), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapGet("/stats", async (ISender s, CancellationToken ct) => (await s.Send(new GetQuestionStatsQuery(), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapGet("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new GetQuestionQuery(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        g.MapPost("/", async (QuestionBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateQuestionCommand(b.ToInput(), b.Submit), ct)).ToCreatedResult(q => $"/api/v1/academic/questions/{q.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, QuestionBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateQuestionCommand(id, b.ToInput(), b.RowVersion), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Update);

        g.MapPost("/{id:guid}/duplicate", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new DuplicateQuestionCommand(id), ct)).ToCreatedResult(q => $"/api/v1/academic/questions/{q.Id}"))
            .RequirePermission(Page, PermissionAction.Create);

        g.MapPost("/import", async (ImportBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ImportQuestionsCommand(b.Rows.Select(r => new ImportRow(r.Row, new QuestionInput(r.Level, r.Skill, r.Format,
                    r.Difficulty, r.Stem, r.Passage, r.Transcript, r.Options, r.Answer, r.Explanation, r.CurriculumId, r.LessonIds, r.Tags))).ToList(),
                    b.Submit), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.Create)
            .WithBodyLimit(ImportLimit);

        // NH-04: file nghe (multipart: file + durationSec)
        g.MapPost("/{id:guid}/audio", async (Guid id, HttpRequest request, ISender s, CancellationToken ct) =>
            {
                if (!request.HasFormContentType) return FileRequired.ToProblem();
                var form = await request.ReadFormAsync(ct);
                if (form.Files.GetFile("file") is not { } file) return FileRequired.ToProblem();
                _ = int.TryParse(form["durationSec"], out var duration);
                await using var stream = file.OpenReadStream();
                return (await s.Send(new UploadQuestionAudioCommand(id, stream, file.FileName, duration), ct)).ToHttpResult();
            })
            .RequirePermission(Page, PermissionAction.Update)
            .WithBodyLimit(AudioLimit)
            .DisableAntiforgery();

        g.MapGet("/{id:guid}/audio-link", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetQuestionAudioLinkQuery(id), ct)).ToHttpResult())
            .RequirePermission(Page, PermissionAction.View);

        // NH-05 / NH-06: hàng loạt (1 hoặc nhiều câu)
        MapStatus(g, "submit", QuestionAction.Submit, PermissionAction.Update);
        MapStatus(g, "approve", QuestionAction.Approve, PermissionAction.Approve);
        MapStatus(g, "return", QuestionAction.Return, PermissionAction.Approve);
        MapStatus(g, "retire", QuestionAction.Retire, PermissionAction.Approve);
        MapStatus(g, "reopen", QuestionAction.Reopen, PermissionAction.Approve);
    }

    private static void MapStatus(RouteGroupBuilder g, string path, QuestionAction action, PermissionAction permission) =>
        g.MapPost($"/{path}", async (IdsBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeQuestionStatusCommand(action, b.Ids, b.Note), ct)).ToHttpResult())
            .RequirePermission(Page, permission);
}
