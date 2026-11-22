using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Exams.GetExamPaper;

/// <summary>Đề + đáp án của kỳ thi (bản in thi giấy, GV xem đề) — chỉ nhân viên / GV của lớp</summary>
public sealed record ExamPaperDto(ClassExamDto Exam, IReadOnlyList<ExamPaperSectionDto> Sections);

/// <summary>ND-04 bước 4 — tải bản in: đề + đáp án riêng (frontend dựng 2 bản in)</summary>
public sealed record GetExamPaperQuery(Guid Id, bool AsTeacher) : IQuery<ExamPaperDto>;

public sealed class GetExamPaperHandler(ExamFinder finder, ExamDrafter drafter, ExamReader reader) : IQueryHandler<GetExamPaperQuery, ExamPaperDto>
{
    public async Task<Result<ExamPaperDto>> Handle(GetExamPaperQuery q, CancellationToken ct)
    {
        var found = await finder.FindAsync(q.Id, q.AsTeacher, PermissionAction.View, ct);
        if (found.IsFailure) return found.Error;
        var (exam, cls) = found.Value;
        return new ExamPaperDto(await reader.OneAsync(exam, cls, includeQuestions: true, ct), await drafter.PaperAsync(exam.Sections, ct));
    }
}
