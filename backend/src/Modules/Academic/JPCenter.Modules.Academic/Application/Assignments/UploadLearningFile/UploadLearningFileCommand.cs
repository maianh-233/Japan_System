using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Assignments.UploadLearningFile;

/// <summary>
/// ND-01 / ND-02 — tải 1 tệp (đề bài GV gửi / bài làm HV, ≤ 20 MB, ảnh / PDF / Word / ghi âm) vào kho riêng tư → trả <c>SubmissionFile</c>
/// để gửi kèm khi giao bài / nộp bài. Quyền do endpoint quyết (nhân viên sửa lớp, GV, HV).
/// </summary>
public sealed record UploadLearningFileCommand(Stream Content, string FileName) : ICommand<SubmissionFileDto>;

public sealed class UploadLearningFileValidator : AbstractValidator<UploadLearningFileCommand>
{
    public UploadLearningFileValidator() => RuleFor(x => x.FileName).NotEmpty().WithMessage("Chọn tệp").MaximumLength(300);
}

public sealed class UploadLearningFileHandler(LearningFiles files) : ICommandHandler<UploadLearningFileCommand, SubmissionFileDto>
{
    public Task<Result<SubmissionFileDto>> Handle(UploadLearningFileCommand cmd, CancellationToken ct) =>
        files.UploadAsync(cmd.Content, cmd.FileName, ct);
}
