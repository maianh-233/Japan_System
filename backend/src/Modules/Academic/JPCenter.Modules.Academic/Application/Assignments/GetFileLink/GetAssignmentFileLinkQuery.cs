using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.GetFileLink;

/// <summary>
/// Link tạm mở 1 tệp: đề bài đính kèm (<paramref name="SubmissionId"/> null) hoặc tệp bài nộp — kiểm quyền xem trước khi cấp link.
/// </summary>
public sealed record GetAssignmentFileLinkQuery(Guid AssignmentId, Guid? SubmissionId, int Index, LearningViewer Viewer, Guid? StudentId = null)
    : IQuery<FileLinkDto>;

public sealed class GetAssignmentFileLinkHandler(IAcademicDbContext db, LearningAccess access, LearningFiles files)
    : IQueryHandler<GetAssignmentFileLinkQuery, FileLinkDto>
{
    public async Task<Result<FileLinkDto>> Handle(GetAssignmentFileLinkQuery q, CancellationToken ct)
    {
        var a = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new AssignmentId(q.AssignmentId), ct);
        if (a is null) return AssignmentErrors.NotFound;

        Guid? onlyStudent = null;
        if (q.Viewer == LearningViewer.Learner)
        {
            var learner = await access.LearnerAsync(q.StudentId, ct);
            if (learner.IsFailure) return learner.Error;
            if (!learner.Value.Enrolled(a.ClassId.Value)) return AssignmentErrors.NotFound;
            onlyStudent = learner.Value.StudentId;
        }
        else
        {
            var cls = await access.ClassAsync(a.ClassId.Value, q.Viewer == LearningViewer.Teacher, PermissionAction.View, ct);
            if (cls.IsFailure) return cls.Error.Type == ErrorType.NotFound ? AssignmentErrors.NotFound : cls.Error;
        }

        if (q.SubmissionId is not { } sid) return await files.LinkAsync(a.Attachments, q.Index, ct);
        var s = await db.Submissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new SubmissionId(sid) && x.AssignmentId == a.Id, ct);
        if (s is null || (onlyStudent is { } me && s.StudentId != me)) return AssignmentErrors.SubmissionNotFound;
        return await files.LinkAsync(s.Files, q.Index, ct);
    }
}
