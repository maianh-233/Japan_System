using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Flashcards.Common;
using JPCenter.Modules.Academic.Domain.Flashcards;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Flashcards.EditFlashCards;

/// <summary>ND-09 — thêm thẻ cuối bộ (nhập nhiều dòng 1 lần). Thẻ thiếu mặt trước / nghĩa → E1 (không thêm thẻ nào).</summary>
public sealed record AddFlashCardsCommand(Guid DeckId, IReadOnlyList<FlashCardInput> Cards) : ICommand<FlashDeckDto>;

/// <summary>ND-09 — sửa 1 thẻ</summary>
public sealed record UpdateFlashCardCommand(Guid DeckId, Guid CardId, FlashCardInput Card) : ICommand<FlashDeckDto>;

/// <summary>ND-09.E2 — xóa thẻ + tiến độ ôn của thẻ đó</summary>
public sealed record RemoveFlashCardCommand(Guid DeckId, Guid CardId) : ICommand<FlashDeckDto>;

/// <summary>ND-09 — sắp xếp lại (đủ mọi thẻ của bộ)</summary>
public sealed record ReorderFlashCardsCommand(Guid DeckId, IReadOnlyList<Guid> CardIds) : ICommand<FlashDeckDto>;

public sealed class AddFlashCardsValidator : AbstractValidator<AddFlashCardsCommand>
{
    public AddFlashCardsValidator() => RuleFor(x => x.Cards).NotEmpty().WithMessage("Nhập ít nhất 1 thẻ").Must(c => c is null || c.Count <= FlashDeck.MaxCards);
}

public sealed class EditFlashCardsHandler(IAcademicDbContext db, FlashDecks decks) :
    ICommandHandler<AddFlashCardsCommand, FlashDeckDto>,
    ICommandHandler<UpdateFlashCardCommand, FlashDeckDto>,
    ICommandHandler<RemoveFlashCardCommand, FlashDeckDto>,
    ICommandHandler<ReorderFlashCardsCommand, FlashDeckDto>
{
    public Task<Result<FlashDeckDto>> Handle(AddFlashCardsCommand cmd, CancellationToken ct) => EditAsync(cmd.DeckId, d =>
    {
        foreach (var c in cmd.Cards)
            if (d.AddCard(c.Front, c.Reading, c.Meaning, c.Example) is { IsFailure: true } r) return r.Error;
        return Result.Success();
    }, ct);

    public Task<Result<FlashDeckDto>> Handle(UpdateFlashCardCommand cmd, CancellationToken ct) => EditAsync(cmd.DeckId,
        d => d.UpdateCard(new FlashCardId(cmd.CardId), cmd.Card.Front, cmd.Card.Reading, cmd.Card.Meaning, cmd.Card.Example), ct);

    public async Task<Result<FlashDeckDto>> Handle(RemoveFlashCardCommand cmd, CancellationToken ct)
    {
        var cardId = new FlashCardId(cmd.CardId);
        var progress = await db.CardProgress.Where(p => p.CardId == cardId).ToListAsync(ct);
        return await EditAsync(cmd.DeckId, d =>
        {
            var r = d.RemoveCard(cardId);
            if (r.IsSuccess) db.CardProgress.RemoveRange(progress);
            return r;
        }, ct);
    }

    public Task<Result<FlashDeckDto>> Handle(ReorderFlashCardsCommand cmd, CancellationToken ct) =>
        EditAsync(cmd.DeckId, d => d.Reorder(cmd.CardIds.Select(i => new FlashCardId(i)).ToList()), ct);

    private async Task<Result<FlashDeckDto>> EditAsync(Guid deckId, Func<FlashDeck, Result> edit, CancellationToken ct)
    {
        var deck = await decks.ForUpdateAsync(deckId, ct);
        if (deck is null) return FlashcardErrors.DeckNotFound;
        var r = edit(deck);
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        return FlashDecks.ToDto(deck);
    }
}
