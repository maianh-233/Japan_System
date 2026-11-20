using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.CareCases.Common;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Application.CareCases.AddCareLog;

/// <summary>
/// HV-14 bước 4 — "Ghi nhận liên hệ": kênh (gọi / Zalo / gặp) + nội dung* (E1) → <c>care_logs</c> (cũng hiện ở tab Học viên của lớp);
/// case "chưa liên hệ" chuyển "đang liên hệ". Chỉ theo Id hoặc lớp × HV (chưa có trường hợp → mở mới).
/// </summary>
public sealed record AddCareLogCommand(CareCaseRef Case, CareChannel Channel, string Note) : ICommand<CareLogDto>;

public sealed class AddCareLogValidator : AbstractValidator<AddCareLogCommand>
{
    public AddCareLogValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public sealed class AddCareLogHandler(IStudentsDbContext db, StudentAccess access, CareCaseOpener opener, ICurrentUser user, IClock clock)
    : ICommandHandler<AddCareLogCommand, CareLogDto>
{
    public async Task<Result<CareLogDto>> Handle(AddCareLogCommand cmd, CancellationToken ct)
    {
        // Kiểm nội dung trước khi mở case mới (không mở case rỗng khi lỗi)
        if (string.IsNullOrWhiteSpace(cmd.Note)) return Error.Validation("students.care_log.note_required", "Chưa nhập nội dung liên hệ");
        var found = await opener.OpenAsync(cmd.Case, ct);
        if (found.IsFailure) return found.Error;
        var c = found.Value;
        var log = CareLog.Create(c.StudentId, c.ClassId, c.BranchId, cmd.Channel, cmd.Note.Trim(), access.ActorId, user.Access?.DisplayName ?? "—",
            clock.UtcNow);
        if (log.IsFailure) return log.Error;
        db.CareLogs.Add(log.Value);
        c.Contacted();
        await db.SaveChangesAsync(ct);
        return CareCaseReader.LogDto(log.Value);
    }
}
