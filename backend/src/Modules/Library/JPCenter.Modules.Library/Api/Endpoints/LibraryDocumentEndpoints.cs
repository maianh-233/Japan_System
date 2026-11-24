using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Api.Common.Validation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Application.Documents.ChangeDocumentStatus;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.Modules.Library.Application.Documents.CreateDocument;
using JPCenter.Modules.Library.Application.Documents.DeleteDocument;
using JPCenter.Modules.Library.Application.Documents.EndDocumentView;
using JPCenter.Modules.Library.Application.Documents.GetDocuments;
using JPCenter.Modules.Library.Application.Documents.GetDocumentStats;
using JPCenter.Modules.Library.Application.Documents.GetPortalLibrary;
using JPCenter.Modules.Library.Application.Documents.SetDocumentAccess;
using JPCenter.Modules.Library.Application.Documents.StartDocumentView;
using JPCenter.Modules.Library.Application.Documents.StartPortalView;
using JPCenter.Modules.Library.Application.Documents.UpdateDocument;
using JPCenter.Modules.Library.Application.Documents.UploadDocumentFile;
using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Library.Api.Endpoints;

/// <summary>
/// User story 16 — thư viện điện tử <c>/api/v1/library/documents</c> (quyền <c>library.documents.*</c>), lượt xem <c>/views</c>,
/// cổng HV / PH <c>/portal/learning/library</c>, cổng GV <c>/portal/teaching/library</c>.
/// </summary>
public static class LibraryDocumentEndpoints
{
    /// <summary>Tệp ≤ 500 MB + phần form</summary>
    public const long UploadLimit = DocumentFiles.MediaMaxBytes + 1024 * 1024;

    private static readonly Error FileRequired = Error.Validation("file.required", "Chọn tệp cần tải lên (form-data, trường \"file\")");

    public sealed record DocBody(string? Title, string? Subtitle, string? Description, DocCategory Category, DocFileType FileType, string? FileKey,
        string? FileName, long? Size, List<JlptLevel>? Levels, List<JapaneseSkill>? Skills, List<string>? Tags, int? Pages, int? Duration,
        string? Publisher, int? Year, List<DocAudience>? Audience, List<Guid>? BranchIds, uint? RowVersion)
    {
        public DocumentInput ToInput() => new(Title, Subtitle, Description, Category, FileType, FileKey, FileName, Size, Levels, Skills, Tags, Pages,
            Duration, Publisher, Year);
    }

    public sealed record AccessBody(List<DocAudience>? Audience, List<Guid>? BranchIds, bool Watermark, bool AllowDownload, uint? RowVersion);

    public static void Map(IEndpointRouteBuilder library)
    {
        const string page = LibraryAccess.Page;
        var docs = library.MapGroup("/documents");

        // TV-01
        docs.MapGet("/", async (Guid? branchId, QueryEnum<DocCategory>? category, QueryEnum<JlptLevel>? level, QueryEnum<JapaneseSkill>? skill,
                    QueryEnum<DocFileType>? fileType, QueryEnum<DocAudience>? audience, string? q, bool? includeHidden, ISender s, CancellationToken ct) =>
                (await s.Send(new GetDocumentsQuery(branchId, category?.Value, level?.Value, skill?.Value, fileType?.Value, audience?.Value, q,
                    includeHidden ?? false), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // TV-02
        docs.MapPost("/files", async (HttpRequest request, ISender s, CancellationToken ct) =>
            {
                if (!request.HasFormContentType) return FileRequired.ToProblem();
                var form = await request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = UploadLimit }, ct);
                if (form.Files.GetFile("file") is not { } file) return FileRequired.ToProblem();
                await using var stream = file.OpenReadStream();
                return (await s.Send(new UploadDocumentFileCommand(stream, file.FileName, file.Length), ct)).ToHttpResult();
            })
            .RequirePermission(page, PermissionAction.Create)
            .WithBodyLimit(UploadLimit)
            .DisableAntiforgery();

        docs.MapPost("/", async (DocBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateDocumentCommand(b.ToInput(), b.Audience, b.BranchIds), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Create);

        // TV-03
        docs.MapPut("/{id:guid}", async (Guid id, DocBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateDocumentCommand(id, b.ToInput(), b.RowVersion ?? 0), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        docs.MapPost("/{id:guid}/hide", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeDocumentStatusCommand(id, DocStatus.Hidden), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        docs.MapPost("/{id:guid}/publish", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeDocumentStatusCommand(id, DocStatus.Published), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        docs.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new DeleteDocumentCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Delete);

        // TV-04
        docs.MapPut("/{id:guid}/access", async (Guid id, AccessBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new SetDocumentAccessCommand(id, b.Audience, b.BranchIds, b.Watermark, b.AllowDownload, b.RowVersion ?? 0), ct))
                .ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        // TV-05, TV-06
        docs.MapPost("/{id:guid}/views", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new StartDocumentViewCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        library.MapPost("/views/{viewId:guid}/end", async (Guid viewId, ISender s, CancellationToken ct) =>
                (await s.Send(new EndDocumentViewCommand(viewId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        docs.MapGet("/{id:guid}/stats", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new GetDocumentStatsQuery(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // TV-07 — cổng HV / PH (PH chọn con: studentId)
        var learning = library.MapGroup("/portal/learning/library").RequirePortal(Portal.Student, Portal.Parent);
        learning.MapGet("/", async (Guid? studentId, QueryEnum<DocCategory>? category, string? q, ISender s, CancellationToken ct) =>
            (await s.Send(new GetPortalLibraryQuery(studentId, category?.Value, q), ct)).ToHttpResult());
        learning.MapPost("/{id:guid}/views", async (Guid id, Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new StartPortalViewCommand(id, studentId), ct)).ToHttpResult());
        learning.MapPost("/views/{viewId:guid}/end", async (Guid viewId, ISender s, CancellationToken ct) =>
            (await s.Send(new EndDocumentViewCommand(viewId), ct)).ToHttpResult());

        // TV-08 — cổng GV
        var teaching = library.MapGroup("/portal/teaching/library").RequirePortal(Portal.Teacher);
        teaching.MapGet("/", async (QueryEnum<DocCategory>? category, string? q, ISender s, CancellationToken ct) =>
            (await s.Send(new GetPortalLibraryQuery(null, category?.Value, q), ct)).ToHttpResult());
        teaching.MapPost("/{id:guid}/views", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new StartPortalViewCommand(id, null), ct)).ToHttpResult());
        teaching.MapPost("/views/{viewId:guid}/end", async (Guid viewId, ISender s, CancellationToken ct) =>
            (await s.Send(new EndDocumentViewCommand(viewId), ct)).ToHttpResult());
    }
}
