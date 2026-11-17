using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.RemoveLesson;

/// <summary>LT-05 — xóa bài (kèm chapter + tài liệu; mốc thi chuyển sang bài liền trước). File không còn ai dùng được dọn khỏi kho</summary>
public sealed record RemoveLessonCommand(Guid CurriculumId, Guid LessonId) : ICommand<CurriculumDto>;

public sealed class RemoveLessonHandler(CurriculumEditor editor) : ICommandHandler<RemoveLessonCommand, CurriculumDto>
{
    public Task<Result<CurriculumDto>> Handle(RemoveLessonCommand cmd, CancellationToken ct) =>
        editor.EditAsync(cmd.CurriculumId, c => c.RemoveLesson(new LessonId(cmd.LessonId)), "Xóa bài học", ct);
}
