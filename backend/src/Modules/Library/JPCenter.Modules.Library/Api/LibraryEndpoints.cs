using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Api.Common.Validation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Library.Application.TeacherDocuments;
using JPCenter.Modules.Library.Application.TeacherDocuments.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Library.Api;

/// <summary>
/// <c>/api/v1/library</c> — giai đoạn 20: tài liệu GV (ND-10). Cổng GV <c>/portal/teaching/documents</c> (chỉ tài liệu mình soạn, lớp mình dạy) ·
/// cổng HV / PH <c>/portal/learning/documents</c> (lớp đã ghi danh). Giai đoạn 23: thư viện điện tử (<see cref="Endpoints.LibraryDocumentEndpoints"/>).
/// </summary>
public static class LibraryEndpoints
{
    /// <summary>Tệp ≤ 50 MB + phần form</summary>
    public const long UploadLimit = 51 * 1024 * 1024;

    private static readonly Error FileRequired = Error.Validation("file.required", "Chọn tệp cần tải lên (form-data, trường \"file\")");

    public sealed record DocBody(string? Title, string? Description, TeacherDocFileDto? File, List<Guid>? ClassIds, Guid? SessionId);

    public static void Map(IEndpointRouteBuilder library)
    {
        var teaching = library.MapGroup("/portal/teaching/documents").RequirePortal(Portal.Teacher);
        teaching.MapGet("/", async (ISender s, CancellationToken ct) => (await s.Send(new GetMyTeacherDocsQuery(), ct)).ToHttpResult());
        teaching.MapPost("/files", async (HttpRequest request, ISender s, CancellationToken ct) =>
            {
                if (!request.HasFormContentType) return FileRequired.ToProblem();
                var form = await request.ReadFormAsync(ct);
                if (form.Files.GetFile("file") is not { } file) return FileRequired.ToProblem();
                await using var stream = file.OpenReadStream();
                return (await s.Send(new UploadTeacherDocFileCommand(stream, file.FileName), ct)).ToHttpResult();
            })
            .WithBodyLimit(UploadLimit)
            .DisableAntiforgery();
        teaching.MapPost("/", async (DocBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new CreateTeacherDocCommand(b.Title, b.Description, b.File, b.ClassIds, b.SessionId), ct)).ToHttpResult());
        teaching.MapPut("/{id:guid}", async (Guid id, DocBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateTeacherDocCommand(id, b.Title, b.Description, b.File, b.ClassIds, b.SessionId), ct)).ToHttpResult());
        teaching.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new DeleteTeacherDocCommand(id), ct)).ToHttpResult());
        teaching.MapGet("/{id:guid}/link", async (Guid id, ISender s, CancellationToken ct) =>
            (await s.Send(new GetTeacherDocLinkQuery(id), ct)).ToHttpResult());

        var learning = library.MapGroup("/portal/learning/documents").RequirePortal(Portal.Student, Portal.Parent);
        learning.MapGet("/", async (Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new GetLearningDocsQuery(studentId), ct)).ToHttpResult());
        learning.MapGet("/{id:guid}/link", async (Guid id, Guid? studentId, ISender s, CancellationToken ct) =>
            (await s.Send(new OpenLearningDocCommand(id, studentId), ct)).ToHttpResult());

        Endpoints.LibraryDocumentEndpoints.Map(library);
    }
}
