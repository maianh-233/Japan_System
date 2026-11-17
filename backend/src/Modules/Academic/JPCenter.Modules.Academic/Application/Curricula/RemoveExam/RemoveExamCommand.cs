using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.RemoveExam;

/// <summary>LT-08 — xóa mốc thi khỏi lộ trình (kỳ thi đã tổ chức không bị ảnh hưởng)</summary>
public sealed record RemoveExamCommand(Guid CurriculumId, Guid ExamId) : ICommand<CurriculumDto>;

public sealed class RemoveExamHandler(CurriculumEditor editor) : ICommandHandler<RemoveExamCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(RemoveExamCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId, c => c.RemoveExam(new ExamMilestoneId(cmd.ExamId)), "Xóa mốc thi", ct);
}
