using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.ClassSessions.ConfirmSessions;

/// <summary>1 buổi không xác nhận được trong lô</summary>
public sealed record ConfirmFailureDto(Guid Id, string Code, string Message);

/// <summary>Kết quả xác nhận hàng loạt</summary>
public sealed record ConfirmSessionsResult(int Confirmed, IReadOnlyList<ConfirmFailureDto> Failed);

/// <summary>DT-15 — xác nhận nhiều buổi 1 lần (1 request): buổi lỗi được bỏ qua và trả lý do, buổi hợp lệ vẫn được xác nhận</summary>
public sealed record ConfirmSessionsCommand(List<Guid> Ids, string? Note) : ICommand<ConfirmSessionsResult>;

public sealed class ConfirmSessionsValidator : AbstractValidator<ConfirmSessionsCommand>
{
    public ConfirmSessionsValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().WithMessage("Chọn buổi cần xác nhận").Must(i => i.Count <= 200).WithMessage("Tối đa 200 buổi mỗi lần");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public sealed class ConfirmSessionsHandler(IAcademicDbContext db, SessionReader reader, SessionConfirmer confirmer, IAuditTrail audit)
    : ICommandHandler<ConfirmSessionsCommand, ConfirmSessionsResult>
{
    public async Task<Result<ConfirmSessionsResult>> Handle(ConfirmSessionsCommand cmd, CancellationToken ct)
    {
        var failed = new List<ConfirmFailureDto>();
        var confirmed = 0;
        foreach (var id in cmd.Ids.Distinct())
        {
            if (await reader.ForUpdateAsync(id, PermissionAction.Approve, ct) is not var (session, cls))
            {
                failed.Add(new ConfirmFailureDto(id, SessionErrors.NotFound.Code, SessionErrors.NotFound.Message));
                continue;
            }
            var done = confirmer.Confirm(session, cls, cmd.Note);
            if (done.IsFailure) failed.Add(new ConfirmFailureDto(id, done.Error.Code, done.Error.Message));
            else confirmed++;
        }
        await db.SaveChangesAsync(ct);
        if (confirmed > 0) audit.Record(AuditCategory.Data, AuditSeverity.Info, "Xác nhận buổi đã dạy (hàng loạt)", $"{confirmed} buổi");
        return new ConfirmSessionsResult(confirmed, failed);
    }
}
