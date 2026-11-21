using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Promotions.Common;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Students.Contracts.Dtos;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Promotions.ConfirmPromotion;

/// <summary>
/// CRM-19 bước 3 — chốt lên lớp: ghi danh HV vào lớp đích (như CRM-07 bước 3–4 — Students kiểm lớp còn nhận / còn chỗ: E4,
/// phát <c>StudentEnrolled</c> cho hóa đơn), đề xuất "Đồng ý". Chưa chọn lớp đích → E2; đã đóng → E1. Gọi kèm <c>Idempotency-Key</c>.
/// </summary>
public sealed record ConfirmPromotionCommand(Guid Id, decimal? Amount, decimal Discount, DateOnly? DueDate) : ICommand<PromotionDto>;

public sealed class ConfirmPromotionHandler(IAcademicDbContext db, SalePromotions sale, PromotionReader reader, AcademicAccess access,
    IStudentEnrollmentService enrollment, IClock clock) : ICommandHandler<ConfirmPromotionCommand, PromotionDto>
{
    public async Task<Result<PromotionDto>> Handle(ConfirmPromotionCommand cmd, CancellationToken ct)
    {
        var p = await sale.ForUpdateAsync(cmd.Id, ct);
        if (p is null) return PromotionErrors.NotFound;
        if (p.IsClosed) return PromotionErrors.Closed;
        if (p.ToClassId is not { } target) return PromotionErrors.ToClassRequired;

        var enrolled = await enrollment.EnrollFromLeadAsync(
            new LeadEnrollmentRequest(p.StudentId, null, target.Value, cmd.Amount, cmd.Discount, cmd.DueDate, "Lên lớp"), ct);
        if (enrolled.IsFailure) return enrolled.Error;
        if (p.Confirm(enrolled.Value.EnrollmentId!.Value, access.ActorId, clock.UtcNow) is { IsFailure: true } bad) return bad.Error;
        await db.SaveChangesAsync(ct);
        return (await reader.ToDtosAsync([p], ct))[0];
    }
}
