using JPCenter.Application.Common.Files;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Library.Domain.TeacherDocuments;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Application.TeacherDocuments.Common;

/// <summary>Nội dung tài liệu khi tạo / đổi: <c>Url</c> = khóa do <c>/files</c> cấp (file) hoặc địa chỉ http(s) (link)</summary>
public sealed record TeacherDocFileDto(string Name, TeacherDocKind Kind, string Url, long? Size);

/// <summary>
/// Tài liệu GV — khớp <c>TeacherDoc</c> (frontend <c>features/portal/types.ts</c>). <c>Url</c> chỉ có với link ngoài (file mở qua link tạm).
/// <c>ViewedBy</c> (HV đã xem) chỉ trả cho GV soạn — cổng HV nhận <c>Viewed</c> của chính mình.
/// </summary>
public sealed record TeacherDocDto(
    Guid Id,
    Guid TeacherId,
    string Title,
    string? Description,
    string FileName,
    TeacherDocKind Kind,
    long? Size,
    string? Url,
    IReadOnlyList<Guid> ClassIds,
    Guid? SessionId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> ViewedBy,
    bool Viewed);

/// <summary>Link mở tài liệu: file → link tạm; link ngoài → địa chỉ gốc (ExpiresAt null)</summary>
public sealed record DocLinkDto(string Url, DateTimeOffset? ExpiresAt);

/// <summary>
/// Dùng chung cho tài liệu GV (ND-10): GV đang gọi + lớp mình dạy (Academic.Contracts — E1), HV ở cổng (PH: các con) + lớp đã ghi danh
/// (Students.Contracts), tải file vào kho riêng tư (≤ 50 MB), cấp link tạm.
/// </summary>
public sealed class TeacherDocs(ICurrentUser user, IClassLookup classes, IEnrollmentLookup enrollments, IGuardianDirectory guardians,
    IFileStorage storage, IClock clock)
{
    public const string Folder = "library/teacher-docs";
    public static readonly TimeSpan LinkValidFor = TimeSpan.FromMinutes(15);

    /// <summary>Tài liệu GV: tài liệu giảng dạy + ảnh — tối đa 50 MB (video: dán link)</summary>
    public static readonly FilePolicy Policy = new("Tài liệu giáo viên", 50 * 1024 * 1024,
        [.. FilePolicy.TeachingMaterials.Types, .. FilePolicy.Images.Types]);

    public static readonly Error InvalidFile = Error.Validation("library.teacher_doc.invalid_file", "Tệp không hợp lệ — tải lại tệp hoặc dán link http(s)");
    public static readonly Error NotTeacher = Error.Forbidden("library.teacher_doc.not_teacher", "Tài khoản chưa gắn hồ sơ giáo viên");

    /// <summary>GV đang gọi (Hr.Employee)</summary>
    public Guid? TeacherId => user.Access?.EmployeeId;

    public bool IsStudent => user.Access?.Portal == Portal.Student;

    /// <summary>ND-10.E1 — mọi lớp phải là lớp GV dạy (GV chính / trợ giảng, chưa hủy)</summary>
    public async Task<Result> CheckClassesAsync(Guid teacherId, IReadOnlyCollection<Guid> classIds, CancellationToken ct)
    {
        var mine = (await classes.TaughtByAsync(teacherId, ct)).Select(c => c.Id).ToHashSet();
        return classIds.All(mine.Contains) ? Result.Success() : TeacherDocumentErrors.ClassNotAllowed;
    }

    /// <summary>HV tài khoản cổng được xem (HV: mình; PH: con — chọn 1 con hoặc tự lấy khi chỉ có 1)</summary>
    public async Task<Result<Guid>> StudentAsync(Guid? studentId, CancellationToken ct)
    {
        IReadOnlyList<Guid> mine = user.Access switch
        {
            { Portal: Portal.Student, OwnerId: { } me } => [me],
            { Portal: Portal.Parent, OwnerId: { } lead } => await guardians.ChildrenOfAsync(lead, ct),
            _ => [],
        };
        if (studentId is { } wanted) return mine.Contains(wanted) ? wanted : TeacherDocumentErrors.NotFound;
        return mine.Count == 1 ? mine[0] : TeacherDocumentErrors.NotFound;
    }

    /// <summary>Lớp HV đã ghi danh (mọi trạng thái)</summary>
    public async Task<IReadOnlyList<Guid>> ClassesOfAsync(Guid studentId, CancellationToken ct) =>
        (await enrollments.OfStudentAsync(studentId, ct)).Select(e => e.ClassId).Distinct().ToList();

    public async Task<Result<TeacherDocFileDto>> UploadAsync(Stream content, string fileName, CancellationToken ct)
    {
        var stored = await storage.UploadAsync(content, fileName, Folder, Policy, ct);
        if (stored.IsFailure) return stored.Error;
        var f = stored.Value;
        return new TeacherDocFileDto(f.FileName, KindOf(f.ContentType), f.Key, f.Size);
    }

    /// <summary>File: khóa trong thư mục tài liệu GV, có thật trong kho (hoặc là file hiện tại); link: http(s)</summary>
    public async Task<Result<TeacherDocFile>> ToFileAsync(TeacherDocFileDto? f, string? currentKey, CancellationToken ct)
    {
        if (f is null || string.IsNullOrWhiteSpace(f.Name) || string.IsNullOrWhiteSpace(f.Url)) return TeacherDocumentErrors.ContentRequired;
        var url = f.Url.Trim();
        if (f.Kind == TeacherDocKind.Link)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)) return InvalidFile;
        }
        else if (url != currentKey && (!url.StartsWith(Folder + "/", StringComparison.Ordinal) || !await storage.ExistsAsync(url, ct))) return InvalidFile;
        return new TeacherDocFile(f.Name, f.Kind, url, f.Kind == TeacherDocKind.Link ? null : f.Size);
    }

    public async Task<DocLinkDto> LinkAsync(TeacherDocument d, CancellationToken ct)
    {
        if (!d.IsStoredFile || d.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return new DocLinkDto(d.Url, null);
        var url = await storage.GetDownloadUrlAsync(d.Url, new FileLinkOptions(LinkValidFor, d.FileName, Inline: true), ct);
        return new DocLinkDto(url.ToString(), clock.UtcNow.Add(LinkValidFor));
    }

    public static TeacherDocDto ToDto(TeacherDocument d, DateTimeOffset createdAt, bool forOwner, Guid? viewer) => new(d.Id.Value, d.TeacherId, d.Title,
        d.Description, d.FileName, d.Kind, d.Size, d.Kind == TeacherDocKind.Link ? d.Url : null, d.ClassIds, d.SessionId, createdAt,
        forOwner ? d.Views.Select(v => v.StudentId).ToList() : [], viewer is { } s && d.Views.Any(v => v.StudentId == s));

    private static TeacherDocKind KindOf(string contentType) => contentType switch
    {
        "application/pdf" => TeacherDocKind.Pdf,
        _ when contentType.StartsWith("image/", StringComparison.Ordinal) => TeacherDocKind.Image,
        _ when contentType.StartsWith("audio/", StringComparison.Ordinal) => TeacherDocKind.Audio,
        _ when contentType.StartsWith("video/", StringComparison.Ordinal) => TeacherDocKind.Video,
        _ => TeacherDocKind.Doc,
    };
}
