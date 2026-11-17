using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Files;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Curricula.UploadMaterial;

/// <summary>
/// LT-06 — tải 1 tệp tài liệu (PDF, Word, PowerPoint, Excel, audio ≤ 50 MB) vào 1 chapter của bản nháp. Kiểm đuôi + nội dung thật
/// (<see cref="FilePolicy.TeachingMaterials"/>), lưu kho riêng tư (ADR 0003). Lưu tài liệu lỗi → xóa file vừa tải (E5).
/// </summary>
public sealed record UploadMaterialCommand(Guid CurriculumId, Guid LessonId, Guid ChapterId, Stream Content, string FileName, string? Name,
    bool SharedWithStudents) : ICommand<CurriculumDto>;

public sealed class UploadMaterialValidator : AbstractValidator<UploadMaterialCommand>
{
    public UploadMaterialValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().WithMessage("Chọn tệp").MaximumLength(300);
        RuleFor(x => x.Name).MaximumLength(300);
    }
}

public sealed class UploadMaterialHandler(IAcademicDbContext db, CurriculumEditor editor, CurriculumReader reader, IFileStorage storage,
    IAuditTrail audit) : ICommandHandler<UploadMaterialCommand, CurriculumDto>
{
    public const string Folder = "academic/materials";

    public async Task<Result<CurriculumDto>> Handle(UploadMaterialCommand cmd, CancellationToken ct)
    {
        var c = await editor.LoadAsync(new CurriculumId(cmd.CurriculumId), ct);
        if (c is null) return CurriculumErrors.NotFound;
        if (!c.IsDraft) return CurriculumErrors.NotDraft; // chặn trước khi tốn công tải file

        var uploaded = await storage.UploadAsync(cmd.Content, cmd.FileName, Folder, FilePolicy.TeachingMaterials, ct);
        if (uploaded.IsFailure) return uploaded.Error;
        var file = uploaded.Value;

        try
        {
            var added = c.AddFileMaterial(new LessonId(cmd.LessonId), new ChapterId(cmd.ChapterId), cmd.Name, file.Key, file.FileName,
                file.ContentType, file.Size, cmd.SharedWithStudents);
            if (added.IsFailure)
            {
                await storage.DeleteAsync(file.Key, ct);
                return added.Error;
            }
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(file.Key, CancellationToken.None);
            throw;
        }

        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Tải tài liệu lên lộ trình", c.Name,
            $"{c.Code} {c.Version} · {file.FileName} ({file.Size / 1024} KB)");
        return (await reader.FindAsync(c.Id, ct))!;
    }
}
