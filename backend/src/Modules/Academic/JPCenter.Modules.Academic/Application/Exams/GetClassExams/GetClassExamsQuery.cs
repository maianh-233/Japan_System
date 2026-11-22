using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Exams.GetClassExams;

/// <summary>Tab Bài thi của lớp (nhân viên <c>academic.classes.view</c> / GV của lớp): mốc thi + phạm vi bài, kỳ thi, kết quả</summary>
public sealed record GetClassExamsQuery(Guid ClassId, bool AsTeacher) : IQuery<ClassExamBoardDto>;

public sealed class GetClassExamsHandler(LearningAccess access, ExamReader reader) : IQueryHandler<GetClassExamsQuery, ClassExamBoardDto>
{
    public async Task<Result<ClassExamBoardDto>> Handle(GetClassExamsQuery q, CancellationToken ct)
    {
        var cls = await access.ClassAsync(q.ClassId, q.AsTeacher, PermissionAction.View, ct);
        if (cls.IsFailure) return cls.Error;
        return await reader.BoardAsync(cls.Value, ct);
    }
}
