using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Flashcards.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Flashcards;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Flashcards.MyFlashcards;

/// <summary>ND-09 — cổng HV / PH: bộ thẻ của các bài trong lộ trình lớp đang học + tiến độ ôn của HV</summary>
public sealed record GetMyFlashcardsQuery(Guid? StudentId) : IQuery<MyFlashcardsDto>;

/// <summary>1 lượt ôn: nhớ → lên hộp (1 → 3 → 7 → 14 → 30 ngày), quên → hộp 0</summary>
public sealed record CardReviewInput(Guid DeckId, Guid CardId, bool Remembered);

/// <summary>ND-09 — HV ghi kết quả ôn (gửi cả phiên 1 lần). Bộ không thuộc lớp đang học → E3.</summary>
public sealed record ReviewFlashcardsCommand(IReadOnlyList<CardReviewInput> Reviews) : ICommand<MyFlashcardsDto>;

public sealed class ReviewFlashcardsValidator : AbstractValidator<ReviewFlashcardsCommand>
{
    public ReviewFlashcardsValidator() => RuleFor(x => x.Reviews).NotEmpty().Must(r => r is null || r.Count <= 500);
}

public sealed class MyFlashcardsHandler(IAcademicDbContext db, LearningAccess access, IClock clock) :
    IQueryHandler<GetMyFlashcardsQuery, MyFlashcardsDto>,
    ICommandHandler<ReviewFlashcardsCommand, MyFlashcardsDto>
{
    public async Task<Result<MyFlashcardsDto>> Handle(GetMyFlashcardsQuery q, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(q.StudentId, ct);
        if (learner.IsFailure) return learner.Error;
        return await BuildAsync(learner.Value, await DecksAsync(learner.Value, ct), ct);
    }

    public async Task<Result<MyFlashcardsDto>> Handle(ReviewFlashcardsCommand cmd, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(null, ct);
        if (learner.IsFailure) return learner.Error;
        var decks = await DecksAsync(learner.Value, ct);
        var byId = decks.ToDictionary(d => d.Id);
        var me = learner.Value.StudentId;
        var cardIds = cmd.Reviews.Select(r => new FlashCardId(r.CardId)).Distinct().ToList();
        var progress = await db.CardProgress.Where(p => p.StudentId == me && cardIds.Contains(p.CardId)).ToDictionaryAsync(p => p.CardId, ct);
        var today = clock.Today;
        var now = clock.UtcNow;
        foreach (var r in cmd.Reviews)
        {
            if (!byId.TryGetValue(new FlashDeckId(r.DeckId), out var deck)) return FlashcardErrors.NotAvailable;
            var cardId = new FlashCardId(r.CardId);
            if (deck.Cards.All(c => c.Id != cardId)) return FlashcardErrors.CardNotFound;
            if (!progress.TryGetValue(cardId, out var p))
            {
                p = CardProgress.Start(me, deck.Id, cardId, today);
                db.CardProgress.Add(p);
                progress[cardId] = p;
            }
            p.Review(r.Remembered, today, now);
        }
        await db.SaveChangesAsync(ct);
        return await BuildAsync(learner.Value, decks, ct);
    }

    /// <summary>Bộ thẻ gắn bài học thuộc lộ trình các lớp đang học</summary>
    private async Task<IReadOnlyList<FlashDeck>> DecksAsync(Learner learner, CancellationToken ct)
    {
        var classIds = learner.StudyingClassIds.Select(c => new ClassId(c)).ToList();
        var curricula = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => classIds.Contains(c.Id) && c.CurriculumId != null)
            .Select(c => c.CurriculumId!.Value).Distinct().ToListAsync(ct);
        if (curricula.Count == 0) return [];
        var lessons = (await db.Curricula.AsNoTracking().AsSplitQuery().IgnoreQueryFilters().Where(c => curricula.Contains(c.Id)).ToListAsync(ct))
            .SelectMany(c => c.Lessons).Select(l => (LessonId?)l.Id).ToList();
        return await db.FlashDecks.AsNoTracking().Where(d => lessons.Contains(d.LessonId)).OrderBy(d => d.Title).ToListAsync(ct);
    }

    private async Task<MyFlashcardsDto> BuildAsync(Learner learner, IReadOnlyList<FlashDeck> decks, CancellationToken ct)
    {
        var me = learner.StudentId;
        var deckIds = decks.Select(d => d.Id).ToList();
        var progress = await db.CardProgress.AsNoTracking().Where(p => p.StudentId == me && deckIds.Contains(p.DeckId)).ToListAsync(ct);
        var today = clock.Today;
        var cards = decks.SelectMany(d => d.Cards).Select(c => c.Id).ToHashSet();
        var started = progress.Where(p => cards.Contains(p.CardId)).ToList();
        var dueToday = started.Count(p => p.Due <= today) + cards.Count - started.Count;

        // Chuỗi ngày học: các ngày (giờ VN) có lượt ôn gần nhất của thẻ, liên tiếp tới hôm nay / hôm qua
        var days = started.Where(p => p.LastReviewedAt is not null).Select(p => TimeText.DateOf(p.LastReviewedAt!.Value)).ToHashSet();
        var streak = 0;
        var d0 = days.Contains(today) ? today : today.AddDays(-1);
        while (days.Contains(d0.AddDays(-streak))) streak++;

        return new MyFlashcardsDto(decks.Select(FlashDecks.ToDto).ToList(),
            started.Select(p => new CardProgressDto(p.CardId.Value, p.Box, p.Due, p.Reviews, p.Lapses)).ToList(), dueToday, streak);
    }
}
