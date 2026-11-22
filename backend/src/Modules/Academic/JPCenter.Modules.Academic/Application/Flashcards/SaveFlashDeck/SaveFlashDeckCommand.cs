using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Flashcards.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Flashcards;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Flashcards.SaveFlashDeck;

/// <summary>
/// ND-09 — tạo (<paramref name="Id"/> null, kèm thẻ ban đầu) / sửa thông tin bộ thẻ (<c>academic.curriculum.update</c>): tên*, trình độ, loại, bài học.
/// Nhãn bài học bỏ trống → lấy tên bài trong lộ trình.
/// </summary>
public sealed record SaveFlashDeckCommand(Guid? Id, string? Title, JlptLevel Level, DeckKind Kind, Guid? LessonId, string? LessonLabel,
    IReadOnlyList<FlashCardInput>? Cards) : ICommand<FlashDeckDto>;

public sealed class SaveFlashDeckValidator : AbstractValidator<SaveFlashDeckCommand>
{
    public SaveFlashDeckValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Nhập tên bộ thẻ").MaximumLength(200);
        RuleFor(x => x.LessonLabel).MaximumLength(100);
        RuleFor(x => x.Level).IsInEnum();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Cards).Must(c => c is null || c.Count <= FlashDeck.MaxCards).WithMessage("Mỗi bộ tối đa 500 thẻ");
    }
}

public sealed class SaveFlashDeckHandler(IAcademicDbContext db, FlashDecks decks) : ICommandHandler<SaveFlashDeckCommand, FlashDeckDto>
{
    public static readonly Error LessonNotFound = Error.Validation("academic.flashcard.lesson_not_found", "Không tìm thấy bài học trong lộ trình");

    public async Task<Result<FlashDeckDto>> Handle(SaveFlashDeckCommand cmd, CancellationToken ct)
    {
        LessonId? lessonId = cmd.LessonId is { } l ? new LessonId(l) : null;
        var label = cmd.LessonLabel;
        if (lessonId is { } lid)
        {
            var lessons = (await db.Curricula.AsNoTracking().AsSplitQuery().ToListAsync(ct)).SelectMany(c => c.Lessons);
            var lesson = lessons.FirstOrDefault(x => x.Id == lid);
            if (lesson is null) return LessonNotFound;
            if (string.IsNullOrWhiteSpace(label)) label = lesson.Title;
        }

        FlashDeck deck;
        if (cmd.Id is { } id)
        {
            var found = await decks.ForUpdateAsync(id, ct);
            if (found is null) return FlashcardErrors.DeckNotFound;
            deck = found;
            if (deck.Update(cmd.Title, cmd.Level, cmd.Kind, lessonId, label) is { IsFailure: true } r) return r.Error;
        }
        else
        {
            var created = FlashDeck.Create(cmd.Title, cmd.Level, cmd.Kind, lessonId, label);
            if (created.IsFailure) return created.Error;
            deck = created.Value;
            foreach (var c in cmd.Cards ?? [])
                if (deck.AddCard(c.Front, c.Reading, c.Meaning, c.Example) is { IsFailure: true } r) return r.Error;
            db.FlashDecks.Add(deck);
        }
        await db.SaveChangesAsync(ct);
        return FlashDecks.ToDto(deck);
    }
}
