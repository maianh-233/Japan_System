using JPCenter.Application.Common.Files;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Questions.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Questions.UploadAudio;

/// <summary>
/// NH-04 — tải / thay file nghe (mp3 / wav / m4a ≤ 20 MB, kiểm cả nội dung — <see cref="FilePolicy.Audio"/>). Chỉ câu nghe hiểu.
/// Lưu lỗi → xóa file vừa tải; thay thành công → xóa file cũ nếu không câu nào khác dùng chung.
/// </summary>
/// <param name="DurationSec">Độ dài (giây) — trình duyệt đọc được khi chọn file</param>
public sealed record UploadQuestionAudioCommand(Guid Id, Stream Content, string FileName, int DurationSec) : ICommand<QuestionDto>;

public sealed class UploadQuestionAudioHandler(IAcademicDbContext db, QuestionBank bank, IFileStorage storage)
    : ICommandHandler<UploadQuestionAudioCommand, QuestionDto>
{
    public async Task<Result<QuestionDto>> Handle(UploadQuestionAudioCommand cmd, CancellationToken ct)
    {
        var q = await bank.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct);
        if (q is null) return QuestionErrors.NotFound;
        // Chặn trước khi tốn công tải file
        if (q.Status == QuestionStatus.Retired) return QuestionErrors.Retired;
        if (q.Skill != JapaneseSkill.Listening) return QuestionErrors.AudioNotListening;
        if (cmd.DurationSec is < 1 or > Question.MaxAudioSeconds) return QuestionErrors.AudioDuration;

        var uploaded = await storage.UploadAsync(cmd.Content, cmd.FileName, AudioFiles.Folder, FilePolicy.Audio, ct);
        if (uploaded.IsFailure) return uploaded.Error;
        var file = uploaded.Value;

        string? old;
        try
        {
            var set = q.SetAudio(file.Key, file.FileName, cmd.DurationSec);
            if (set.IsFailure)
            {
                await storage.DeleteAsync(file.Key, ct);
                return set.Error;
            }
            old = set.Value;
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(file.Key, CancellationToken.None);
            throw;
        }

        await AudioFiles.DeleteIfUnusedAsync(db, storage, old, ct);
        return await bank.ToDtoAsync(q, ct);
    }
}
