using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Library.Application.Documents.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Application.Documents.UploadDocumentFile;

/// <summary>TV-02 bước 1 — tải 1 tệp (PDF ≤ 100 MB, audio / video ≤ 500 MB) vào kho → khóa gửi kèm khi thêm / sửa tài liệu</summary>
public sealed record UploadDocumentFileCommand(Stream Content, string FileName, long Length) : ICommand<UploadedDocFileDto>;

public sealed class UploadDocumentFileValidator : AbstractValidator<UploadDocumentFileCommand>
{
    public UploadDocumentFileValidator() => RuleFor(x => x.FileName).NotEmpty().WithMessage("Chọn tệp").MaximumLength(300);
}

public sealed class UploadDocumentFileHandler(DocumentFiles files) : ICommandHandler<UploadDocumentFileCommand, UploadedDocFileDto>
{
    public Task<Result<UploadedDocFileDto>> Handle(UploadDocumentFileCommand cmd, CancellationToken ct) =>
        files.UploadAsync(cmd.Content, cmd.FileName, cmd.Length, ct);
}
