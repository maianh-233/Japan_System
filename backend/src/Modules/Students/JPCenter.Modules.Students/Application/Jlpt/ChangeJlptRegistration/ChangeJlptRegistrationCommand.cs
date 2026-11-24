using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.ChangeJlptRegistration;

/// <summary>
/// HV-16 bước 2–3 — đóng phí → nộp hội đồng (đủ giấy tờ — E2) → số báo danh → điểm (tự tính đỗ) / vắng; hủy trước khi có kết quả (E3);
/// chứng chỉ gốc về (chỉ lượt đỗ — E4) → phát 1 lần (E5). Dữ liệu kèm theo tùy bước: <paramref name="AdmitNo"/>, <paramref name="Scores"/>,
/// <paramref name="Note"/> (hủy), <paramref name="CertNo"/>, <paramref name="HandedTo"/>.
/// </summary>
public sealed record ChangeJlptRegistrationCommand(Guid Id, JlptStep Step, string? AdmitNo = null, IReadOnlyList<int>? Scores = null,
    string? Note = null, string? CertNo = null, string? HandedTo = null) : ICommand<JlptRegistrationDto>;

public sealed class ChangeJlptRegistrationHandler(IStudentsDbContext db, JlptReader reader, StudentAccess access, IClock clock)
    : ICommandHandler<ChangeJlptRegistrationCommand, JlptRegistrationDto>
{
    public async Task<Result<JlptRegistrationDto>> Handle(ChangeJlptRegistrationCommand cmd, CancellationToken ct)
    {
        var r = await reader.Scoped(PermissionAction.Update).FirstOrDefaultAsync(x => x.Id == new JlptRegistrationId(cmd.Id), ct);
        if (r is null) return JlptErrors.NotFound;
        var now = clock.UtcNow;
        var result = cmd.Step switch
        {
            JlptStep.Pay => r.MarkPaid(now),
            JlptStep.Submit => r.MarkSubmitted(),
            JlptStep.Admit => r.SetAdmit(cmd.AdmitNo),
            JlptStep.Result => r.RecordResult(cmd.Scores ?? []),
            JlptStep.Absent => r.MarkAbsent(),
            JlptStep.Cancel => r.Cancel(cmd.Note),
            JlptStep.CertReceived => r.ReceiveCertificate(cmd.CertNo, now, access.ActorId),
            _ => r.HandCertificate(now, access.ActorId, cmd.HandedTo),
        };
        if (result.IsFailure) return result.Error;
        await db.SaveChangesAsync(ct);
        return await reader.ReloadAsync(r.Id, ct);
    }
}
