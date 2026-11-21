using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Promotions.SaleConsult;

/// <summary>Thao tác tư vấn lên lớp không ghi danh (CRM-19 bước 3)</summary>
public enum SaleConsultAction
{
    /// <summary>Ghi chú tư vấn (kênh, nội dung) — chờ tư vấn → đang tư vấn</summary>
    Note,
    /// <summary>Không liên lạc được</summary>
    Unreachable,
    /// <summary>Từ chối — bắt buộc lý do, trả chỗ</summary>
    Decline,
}

/// <summary>CRM-19 — ghi chú / không liên lạc được / từ chối. Đề xuất đã chốt / từ chối → <c>academic.promotion.closed</c> (E1).</summary>
public sealed record SaleConsultCommand(Guid Id, SaleConsultAction Action, string? Text, string? Channel) : ICommand<PromotionDto>;

public sealed class SaleConsultHandler(IAcademicDbContext db, SalePromotions sale, PromotionReader reader, AcademicAccess access, IClock clock)
    : ICommandHandler<SaleConsultCommand, PromotionDto>
{
    public async Task<Result<PromotionDto>> Handle(SaleConsultCommand cmd, CancellationToken ct)
    {
        var p = await sale.ForUpdateAsync(cmd.Id, ct);
        if (p is null) return PromotionErrors.NotFound;
        var done = cmd.Action switch
        {
            SaleConsultAction.Note => p.AddNote(cmd.Text ?? "", cmd.Channel, access.ActorId, access.ActorName, clock.UtcNow),
            SaleConsultAction.Unreachable => p.MarkUnreachable(),
            _ => p.Decline(cmd.Text ?? ""),
        };
        if (done.IsFailure) return done.Error;
        await db.SaveChangesAsync(ct);
        return (await reader.ToDtosAsync([p], ct))[0];
    }
}
