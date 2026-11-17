using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.ReorderLessons;

/// <summary>LT-05.E4 — đổi thứ tự bài: gửi đủ Id các bài theo thứ tự mới trong 1 request</summary>
public sealed record ReorderLessonsCommand(Guid CurriculumId, List<Guid> LessonIds) : ICommand<CurriculumDto>;

public sealed class ReorderLessonsHandler(CurriculumEditor editor) : ICommandHandler<ReorderLessonsCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(ReorderLessonsCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId, c => c.ReorderLessons(cmd.LessonIds.Select(x => new LessonId(x)).ToList()), "Đổi thứ tự bài học", ct);
}
