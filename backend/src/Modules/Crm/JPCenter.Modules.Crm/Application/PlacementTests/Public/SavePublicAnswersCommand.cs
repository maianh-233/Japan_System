using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.PlacementTests.Common;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.PlacementTests.Public;

/// <summary>Lưu dần đáp án khi đang làm (rớt mạng mở lại vẫn còn; hết hạn thì job chấm phần đã lưu)</summary>
public sealed record SavePublicAnswersCommand(string Token, Dictionary<Guid, int>? Answers) : ICommand;

public sealed class SavePublicAnswersHandler(ICrmDbContext db, PublicAttempts attempts) : ICommandHandler<SavePublicAnswersCommand>
{
    public async Task<Result> Handle(SavePublicAnswersCommand cmd, CancellationToken ct)
    {
        var found = await attempts.FindAsync(cmd.Token, ct);
        if (found is null) return PublicAttempts.NotFound;
        var (attempt, test) = found.Value;
        if (attempt.SaveAnswers(PublicAttempts.Clean(test, cmd.Answers)) is { IsFailure: true } bad) return bad;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
