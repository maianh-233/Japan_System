using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.PlacementTests.Public;

/// <summary>
/// CRM-13 bước 3 — nộp (hoặc hết giờ tự nộp): chấm đúng / tổng, theo kỹ năng, mốc gợi ý trình độ + khóa; ghi lượt làm cho thống kê câu hỏi.
/// Chưa bắt đầu / đã nộp → E4. Lead chỉ thấy điểm tổng.
/// </summary>
public sealed record SubmitPublicAttemptCommand(string Token, Dictionary<Guid, int>? Answers) : ICommand<PublicAttemptDto>;

public sealed class SubmitPublicAttemptHandler(ICrmDbContext db, PublicAttempts attempts, PlacementBank bank, IClock clock)
    : ICommandHandler<SubmitPublicAttemptCommand, PublicAttemptDto>
{
    public async Task<Result<PublicAttemptDto>> Handle(SubmitPublicAttemptCommand cmd, CancellationToken ct)
    {
        var found = await attempts.FindAsync(cmd.Token, ct);
        if (found is null) return PublicAttempts.NotFound;
        var (attempt, test) = found.Value;
        if (attempt.Status != Domain.PlacementTests.AttemptStatus.InProgress) return Domain.PlacementTests.PlacementAttemptErrors.NotInProgress;
        var answers = PublicAttempts.Clean(test, cmd.Answers);
        var (score, band) = await bank.GradeAsync(test, answers, record: true, ct);
        var now = clock.UtcNow;
        if (attempt.Submit(answers, score, band, now) is { IsFailure: true } bad) return bad.Error;
        await db.SaveChangesAsync(ct);
        return await attempts.ToDtoAsync(attempt, test, now, ct);
    }
}
