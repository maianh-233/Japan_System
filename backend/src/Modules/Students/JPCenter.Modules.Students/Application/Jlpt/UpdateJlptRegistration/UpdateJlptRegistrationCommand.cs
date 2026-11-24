using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.UpdateJlptRegistration;

/// <summary>
/// HV-16 — sửa lượt đăng ký: cấp độ / địa điểm và checklist hồ sơ chỉ trước khi nộp hội đồng (E3); ghi chú sửa lúc nào cũng được.
/// Chỉ đổi phần có thay đổi (sau khi nộp vẫn ghi chú được).
/// </summary>
public sealed record UpdateJlptRegistrationCommand(Guid Id, JlptLevel Level, string? Venue, JlptDocsDto? Docs, string? Note) : ICommand<JlptRegistrationDto>;

public sealed class UpdateJlptRegistrationHandler(IStudentsDbContext db, JlptReader reader) : ICommandHandler<UpdateJlptRegistrationCommand, JlptRegistrationDto>
{
    public async Task<Result<JlptRegistrationDto>> Handle(UpdateJlptRegistrationCommand cmd, CancellationToken ct)
    {
        var r = await reader.Scoped(PermissionAction.Update).FirstOrDefaultAsync(x => x.Id == new JlptRegistrationId(cmd.Id), ct);
        if (r is null) return JlptErrors.NotFound;
        if (cmd.Level != r.Level || cmd.Venue?.Trim() != r.Venue)
        {
            var session = await db.JlptSessions.AsNoTracking().FirstAsync(s => s.Id == r.SessionId, ct);
            if (r.ChangeExam(session, cmd.Level, cmd.Venue) is { IsFailure: true } bad) return bad.Error;
        }
        if (cmd.Docs is { } d && (d.Photo, d.IdCard, d.Form) != (r.DocPhoto, r.DocIdCard, r.DocForm)
            && r.SetDocuments(d.Photo, d.IdCard, d.Form) is { IsFailure: true } docs)
            return docs.Error;
        r.SetNote(cmd.Note);
        await db.SaveChangesAsync(ct);
        return await reader.ReloadAsync(r.Id, ct);
    }
}
