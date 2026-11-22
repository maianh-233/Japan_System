using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Library.Application.Abstractions;
using JPCenter.Modules.Library.Application.TeacherDocuments.Common;
using JPCenter.Modules.Library.Domain.TeacherDocuments;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Library.Application.TeacherDocuments;

/// <summary>ND-10 — cổng GV › Tài liệu của tôi (mới nhất trước, kèm HV đã xem)</summary>
public sealed record GetMyTeacherDocsQuery : IQuery<IReadOnlyList<TeacherDocDto>>;

/// <summary>ND-10 — tải 1 tệp (≤ 50 MB) → khóa để gửi kèm khi tạo / đổi tài liệu</summary>
public sealed record UploadTeacherDocFileCommand(Stream Content, string FileName) : ICommand<TeacherDocFileDto>;

/// <summary>ND-10 — GV tạo tài liệu: tiêu đề*, mô tả, file / link*, lớp* (chỉ lớp mình dạy — E1), buổi học</summary>
public sealed record CreateTeacherDocCommand(string? Title, string? Description, TeacherDocFileDto? File, IReadOnlyList<Guid>? ClassIds, Guid? SessionId)
    : ICommand<TeacherDocDto>;

/// <summary>ND-10 — GV sửa tài liệu của mình; <c>File</c> khác file hiện tại → lượt xem về 0</summary>
public sealed record UpdateTeacherDocCommand(Guid Id, string? Title, string? Description, TeacherDocFileDto? File, IReadOnlyList<Guid>? ClassIds,
    Guid? SessionId) : ICommand<TeacherDocDto>;

/// <summary>ND-10.E3 — xóa mềm (Thùng rác); file trong kho giữ tới khi dọn Thùng rác</summary>
public sealed record DeleteTeacherDocCommand(Guid Id) : ICommand;

/// <summary>GV mở tài liệu của mình</summary>
public sealed record GetTeacherDocLinkQuery(Guid Id) : IQuery<DocLinkDto>;

public sealed class UploadTeacherDocFileValidator : AbstractValidator<UploadTeacherDocFileCommand>
{
    public UploadTeacherDocFileValidator() => RuleFor(x => x.FileName).NotEmpty().WithMessage("Chọn tệp").MaximumLength(300);
}

public sealed class CreateTeacherDocValidator : AbstractValidator<CreateTeacherDocCommand>
{
    public CreateTeacherDocValidator()
    {
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ClassIds).Must(c => c is null || c.Count <= 50);
    }
}

public sealed class UpdateTeacherDocValidator : AbstractValidator<UpdateTeacherDocCommand>
{
    public UpdateTeacherDocValidator()
    {
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ClassIds).Must(c => c is null || c.Count <= 50);
    }
}

public sealed class TeacherDocHandlers(ILibraryDbContext db, TeacherDocs docs) :
    IQueryHandler<GetMyTeacherDocsQuery, IReadOnlyList<TeacherDocDto>>,
    ICommandHandler<UploadTeacherDocFileCommand, TeacherDocFileDto>,
    ICommandHandler<CreateTeacherDocCommand, TeacherDocDto>,
    ICommandHandler<UpdateTeacherDocCommand, TeacherDocDto>,
    ICommandHandler<DeleteTeacherDocCommand>,
    IQueryHandler<GetTeacherDocLinkQuery, DocLinkDto>
{
    public async Task<Result<IReadOnlyList<TeacherDocDto>>> Handle(GetMyTeacherDocsQuery q, CancellationToken ct)
    {
        if (docs.TeacherId is not { } me) return TeacherDocs.NotTeacher;
        var rows = await db.TeacherDocuments.AsNoTracking().Where(d => d.TeacherId == me)
            .Select(d => new { D = d, Created = EF.Property<DateTimeOffset>(d, "CreatedAt") }).ToListAsync(ct);
        return rows.OrderByDescending(r => r.Created).Select(r => TeacherDocs.ToDto(r.D, r.Created, forOwner: true, null)).ToList();
    }

    public async Task<Result<TeacherDocFileDto>> Handle(UploadTeacherDocFileCommand cmd, CancellationToken ct)
    {
        if (docs.TeacherId is null) return TeacherDocs.NotTeacher;
        return await docs.UploadAsync(cmd.Content, cmd.FileName, ct);
    }

    public async Task<Result<TeacherDocDto>> Handle(CreateTeacherDocCommand cmd, CancellationToken ct)
    {
        if (docs.TeacherId is not { } me) return TeacherDocs.NotTeacher;
        var classIds = cmd.ClassIds?.Distinct().ToList() ?? [];
        if (await docs.CheckClassesAsync(me, classIds, ct) is { IsFailure: true } denied) return denied.Error;
        var file = await docs.ToFileAsync(cmd.File, null, ct);
        if (file.IsFailure) return file.Error;
        var created = TeacherDocument.Create(me, cmd.Title, cmd.Description, file.Value, classIds, cmd.SessionId);
        if (created.IsFailure) return created.Error;
        db.TeacherDocuments.Add(created.Value);
        await db.SaveChangesAsync(ct);
        return await ReloadAsync(created.Value.Id, ct);
    }

    public async Task<Result<TeacherDocDto>> Handle(UpdateTeacherDocCommand cmd, CancellationToken ct)
    {
        if (docs.TeacherId is not { } me) return TeacherDocs.NotTeacher;
        var d = await db.TeacherDocuments.FirstOrDefaultAsync(x => x.Id == new TeacherDocumentId(cmd.Id), ct);
        if (d is null) return TeacherDocumentErrors.NotFound;
        if (d.TeacherId != me) return TeacherDocumentErrors.NotOwner;
        var classIds = cmd.ClassIds?.Distinct().ToList() ?? [];
        // Lớp đã gửi trước đó (vd lớp đã kết thúc) giữ được; lớp mới thêm phải là lớp mình dạy
        if (await docs.CheckClassesAsync(me, classIds.Except(d.ClassIds).ToList(), ct) is { IsFailure: true } denied) return denied.Error;

        TeacherDocFile? newFile = null;
        if (cmd.File is { } f && (f.Url.Trim() != d.Url || f.Kind != d.Kind))
        {
            var file = await docs.ToFileAsync(f, null, ct);
            if (file.IsFailure) return file.Error;
            newFile = file.Value;
        }
        var r = d.Update(me, cmd.Title, cmd.Description, newFile, classIds, cmd.SessionId);
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        return await ReloadAsync(d.Id, ct);
    }

    public async Task<Result> Handle(DeleteTeacherDocCommand cmd, CancellationToken ct)
    {
        if (docs.TeacherId is not { } me) return TeacherDocs.NotTeacher;
        var d = await db.TeacherDocuments.FirstOrDefaultAsync(x => x.Id == new TeacherDocumentId(cmd.Id), ct);
        if (d is null) return TeacherDocumentErrors.NotFound;
        if (d.TeacherId != me) return TeacherDocumentErrors.NotOwner;
        db.TeacherDocuments.Remove(d);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<DocLinkDto>> Handle(GetTeacherDocLinkQuery q, CancellationToken ct)
    {
        if (docs.TeacherId is not { } me) return TeacherDocs.NotTeacher;
        var d = await db.TeacherDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new TeacherDocumentId(q.Id) && x.TeacherId == me, ct);
        return d is null ? TeacherDocumentErrors.NotFound : await docs.LinkAsync(d, ct);
    }

    private async Task<TeacherDocDto> ReloadAsync(TeacherDocumentId id, CancellationToken ct)
    {
        var r = await db.TeacherDocuments.AsNoTracking().Where(d => d.Id == id)
            .Select(d => new { D = d, Created = EF.Property<DateTimeOffset>(d, "CreatedAt") }).FirstAsync(ct);
        return TeacherDocs.ToDto(r.D, r.Created, forOwner: true, null);
    }
}
