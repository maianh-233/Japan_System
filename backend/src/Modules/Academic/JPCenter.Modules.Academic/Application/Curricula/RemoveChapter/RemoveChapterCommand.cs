using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.RemoveChapter;

/// <summary>LT-05 — xóa chapter (kèm tài liệu; file không còn ai dùng được dọn khỏi kho)</summary>
public sealed record RemoveChapterCommand(Guid CurriculumId, Guid LessonId, Guid ChapterId) : ICommand<CurriculumDto>;

public sealed class RemoveChapterHandler(CurriculumEditor editor) : ICommandHandler<RemoveChapterCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(RemoveChapterCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId, c => c.RemoveChapter(new LessonId(cmd.LessonId), new ChapterId(cmd.ChapterId)), "Xóa chapter", ct);
}
