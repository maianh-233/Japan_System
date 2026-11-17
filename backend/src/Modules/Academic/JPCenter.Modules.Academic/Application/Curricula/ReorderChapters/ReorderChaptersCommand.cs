using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.ReorderChapters;

/// <summary>LT-05.E4 — đổi thứ tự chapter trong 1 bài: gửi đủ Id theo thứ tự mới</summary>
public sealed record ReorderChaptersCommand(Guid CurriculumId, Guid LessonId, List<Guid> ChapterIds) : ICommand<CurriculumDto>;

public sealed class ReorderChaptersHandler(CurriculumEditor editor) : ICommandHandler<ReorderChaptersCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(ReorderChaptersCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId,
            c => c.ReorderChapters(new LessonId(cmd.LessonId), cmd.ChapterIds.Select(x => new ChapterId(x)).ToList()), "Đổi thứ tự chapter", ct);
}
