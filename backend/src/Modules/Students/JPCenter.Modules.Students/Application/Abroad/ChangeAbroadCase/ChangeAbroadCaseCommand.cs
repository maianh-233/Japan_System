using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abroad.Common;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Application.Abroad.ChangeAbroadCase;

/// <summary>
/// HV-17 bước 2–4 — sang bước kế (từng bước, tự ghi chú — E3, E4), giấy tờ chưa có → đã nhận → đã kiểm tra, ngày phỏng vấn / COE / visa / bay
/// (ngày nhận ≥ ngày nộp — E6), đóng phí (không vượt tổng — E6), ghi chú, dừng (lý do), mở lại (E5). Hồ sơ đã dừng chỉ ghi chú / mở lại (E1).
/// Không kiểm <c>xmin</c>: mỗi thao tác tự kiểm điều kiện của nó.
/// </summary>
public sealed record ChangeAbroadCaseCommand(Guid Id, AbroadStep Step, Guid? DocumentId = null, DocumentState? State = null,
    AbroadMilestones? Milestones = null, decimal? Amount = null, string? Text = null) : ICommand<AbroadCaseDto>;

public sealed class ChangeAbroadCaseHandler(IStudentsDbContext db, AbroadDesk desk, StudentAccess access, IClock clock)
    : ICommandHandler<ChangeAbroadCaseCommand, AbroadCaseDto>
{
    public async Task<Result<AbroadCaseDto>> Handle(ChangeAbroadCaseCommand cmd, CancellationToken ct)
    {
        var c = await desk.FindAsync(cmd.Id, PermissionAction.Update, ct);
        if (c is null) return AbroadErrors.NotFound;
        var (by, name, now) = (access.ActorId, access.ActorName, clock.UtcNow);
        var r = cmd.Step switch
        {
            AbroadStep.Advance => c.Advance(by, name, now),
            AbroadStep.Document when cmd.DocumentId is { } doc && cmd.State is { } state => c.SetDocumentState(doc, state),
            AbroadStep.Document => AbroadErrors.DocumentNotFound,
            AbroadStep.Milestones => Milestones(c, cmd.Milestones ?? new AbroadMilestones(null, null, null, null, null, null, null)),
            AbroadStep.Payment => Money.Create(cmd.Amount ?? 0) is { IsSuccess: true } m ? c.RecordPayment(m.Value) : AbroadErrors.InvalidPayment,
            AbroadStep.Note => c.AddNote(cmd.Text, by, name, now),
            AbroadStep.Cancel => c.Cancel(cmd.Text, by, name, now),
            _ => c.Reopen(by, name, now),
        };
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        return await desk.ReloadAsync(c.Id, ct);
    }

    private static Result Milestones(AbroadCase c, AbroadMilestones m)
    {
        foreach (var step in new Func<Result>[]
                 {
                     () => c.RecordInterview(m.InterviewDate, m.InterviewResult),
                     () => c.RecordCoe(m.CoeSubmittedAt, m.CoeGrantedAt),
                     () => c.RecordVisa(m.VisaAppliedAt, m.VisaIssuedAt),
                     () => c.SetFlight(m.FlightDate),
                 })
        {
            if (step() is { IsFailure: true } bad) return bad;
        }
        return Result.Success();
    }
}
