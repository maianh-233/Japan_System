using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Jlpt.ChangeJlptRegistration;
using JPCenter.Modules.Students.Application.Jlpt.Common;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Jlpt.ChangeJlptRegistrations;

/// <summary>
/// HV-16 bước 2 — thao tác hàng loạt trên trang (chọn nhiều HV): đóng lệ phí / đã nộp hội đồng. Lượt không chuyển được (sai thứ tự,
/// thiếu giấy tờ, ngoài phạm vi) → <c>skipped</c> kèm mã lỗi, phần còn lại vẫn cập nhật. Số báo danh / điểm nhập từng người.
/// </summary>
public sealed record ChangeJlptRegistrationsCommand(IReadOnlyList<Guid> Ids, JlptStep Step) : ICommand<JlptBulkResult>;

public sealed class ChangeJlptRegistrationsValidator : AbstractValidator<ChangeJlptRegistrationsCommand>
{
    public ChangeJlptRegistrationsValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().WithMessage("Chọn ít nhất 1 lượt đăng ký").Must(x => x.Count <= 200).WithMessage("Tối đa 200 lượt / lần");
        RuleFor(x => x.Step).Must(s => s is JlptStep.Pay or JlptStep.Submit).WithMessage("Chỉ đóng phí / nộp hội đồng hàng loạt");
    }
}

public sealed class ChangeJlptRegistrationsHandler(IStudentsDbContext db, JlptReader reader, IClock clock)
    : ICommandHandler<ChangeJlptRegistrationsCommand, JlptBulkResult>
{
    public async Task<Result<JlptBulkResult>> Handle(ChangeJlptRegistrationsCommand cmd, CancellationToken ct)
    {
        var ids = cmd.Ids.Distinct().Select(i => new JlptRegistrationId(i)).ToList();
        var regs = await reader.Scoped(PermissionAction.Update).Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var changed = new List<JlptRegistrationId>();
        var skipped = new List<JlptBulkSkipped>();
        foreach (var id in ids)
        {
            if (!regs.TryGetValue(id, out var r))
            {
                skipped.Add(new JlptBulkSkipped(id.Value, JlptErrors.NotFound.Code, JlptErrors.NotFound.Message));
                continue;
            }
            var result = cmd.Step == JlptStep.Pay ? r.MarkPaid(clock.UtcNow) : r.MarkSubmitted();
            if (result.IsFailure) skipped.Add(new JlptBulkSkipped(id.Value, result.Error.Code, result.Error.Message));
            else changed.Add(id);
        }
        await db.SaveChangesAsync(ct);
        var dtos = changed.Count == 0 ? [] : await reader.ToDtosAsync(db.JlptRegistrations.Where(r => changed.Contains(r.Id)), ct);
        return new JlptBulkResult(dtos, skipped);
    }
}
