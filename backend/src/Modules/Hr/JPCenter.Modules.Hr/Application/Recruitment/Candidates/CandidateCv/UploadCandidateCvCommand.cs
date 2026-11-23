using JPCenter.Application.Common.Files;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Recruitment.Common;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Recruitment.Candidates.CandidateCv;

/// <summary>TD-02 — tải CV (PDF / Word ≤ 10 MB) vào kho riêng tư; tải lại thay file cũ.</summary>
public sealed record UploadCandidateCvCommand(Guid Id, Stream Content, string FileName) : ICommand<CandidateResult>;

/// <summary>Link tạm mở CV (sau khi kiểm quyền xem tuyển dụng)</summary>
public sealed record GetCandidateCvLinkQuery(Guid Id) : IQuery<CvLink>;

public sealed record CvLink(string Url, string Name);

public sealed class UploadCandidateCvHandler(IHrDbContext db, RecruitmentDesk desk, IFileStorage files)
    : ICommandHandler<UploadCandidateCvCommand, CandidateResult>
{
    public const string Folder = "hr/cv";
    public static readonly FilePolicy CvPolicy = new("CV ứng viên", 10 * 1024 * 1024, [FileTypes.Pdf, FileTypes.Doc, FileTypes.Docx]);
    public static readonly Error NoCv = Error.NotFound("hr.candidate.no_cv", "Ứng viên chưa có CV");

    public async Task<Result<CandidateResult>> Handle(UploadCandidateCvCommand cmd, CancellationToken ct)
    {
        var found = await desk.FindCandidateAsync(cmd.Id, PermissionAction.Update, ct);
        if (found is null) return RecruitmentErrors.CandidateNotFound;
        var (c, job) = found.Value;
        var stored = await files.UploadAsync(cmd.Content, cmd.FileName, Folder, CvPolicy, ct);
        if (stored.IsFailure) return stored.Error;
        var old = c.CvKey;
        c.AttachCv(stored.Value.Key, Path.GetFileName(cmd.FileName));
        await db.SaveChangesAsync(ct);
        if (old is not null && old != stored.Value.Key) await files.DeleteAsync(old, ct);
        return await desk.ResultAsync(c, job, null, [], ct);
    }
}

public sealed class GetCandidateCvLinkHandler(RecruitmentDesk desk, IFileStorage files) : IQueryHandler<GetCandidateCvLinkQuery, CvLink>
{
    public async Task<Result<CvLink>> Handle(GetCandidateCvLinkQuery q, CancellationToken ct)
    {
        var found = await desk.FindCandidateAsync(q.Id, PermissionAction.View, ct);
        if (found is null) return RecruitmentErrors.CandidateNotFound;
        var c = found.Value.Candidate;
        if (c.CvKey is not { } key) return UploadCandidateCvHandler.NoCv;
        var url = await files.GetDownloadUrlAsync(key, new FileLinkOptions(DownloadName: c.CvName), ct);
        return new CvLink(url.ToString(), c.CvName ?? "CV");
    }
}
