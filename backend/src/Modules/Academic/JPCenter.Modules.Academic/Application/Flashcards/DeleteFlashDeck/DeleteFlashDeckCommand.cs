using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Flashcards;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Flashcards.DeleteFlashDeck;

/// <summary>ND-09 — xóa bộ thẻ (xóa mềm — Thùng rác; tiến độ ôn giữ để khôi phục)</summary>
public sealed record DeleteFlashDeckCommand(Guid Id) : ICommand;

public sealed class DeleteFlashDeckHandler(IAcademicDbContext db) : ICommandHandler<DeleteFlashDeckCommand>
{
    public async Task<Result> Handle(DeleteFlashDeckCommand cmd, CancellationToken ct)
    {
        var deck = await db.FlashDecks.FirstOrDefaultAsync(d => d.Id == new FlashDeckId(cmd.Id), ct);
        if (deck is null) return FlashcardErrors.DeckNotFound;
        db.FlashDecks.Remove(deck);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
