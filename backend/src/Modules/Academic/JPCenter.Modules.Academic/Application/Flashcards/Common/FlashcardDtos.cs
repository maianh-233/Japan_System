using JPCenter.Modules.Academic.Domain.Flashcards;

namespace JPCenter.Modules.Academic.Application.Flashcards.Common;

/// <summary>1 thẻ — khớp <c>FlashCard</c> (frontend <c>features/portal/types.ts</c>)</summary>
public sealed record FlashCardDto(Guid Id, string Front, string? Reading, string Meaning, string? Example, int Order);

/// <summary>Bộ thẻ — khớp <c>FlashDeck</c> (frontend): <c>Level</c> "N5"…"N1", <c>Lesson</c> = nhãn bài học</summary>
public sealed record FlashDeckDto(Guid Id, string Title, string Level, DeckKind Kind, Guid? LessonId, string Lesson, IReadOnlyList<FlashCardDto> Cards);

/// <summary>Tiến độ ôn 1 thẻ — khớp <c>CardProgress</c> (frontend)</summary>
public sealed record CardProgressDto(Guid CardId, int Box, DateOnly Due, int Reviews, int Lapses);

/// <summary>Cổng HV: bộ thẻ của các bài trong lộ trình lớp đang học + tiến độ ôn, số thẻ đến hạn hôm nay, chuỗi ngày học</summary>
public sealed record MyFlashcardsDto(IReadOnlyList<FlashDeckDto> Decks, IReadOnlyList<CardProgressDto> Progress, int DueToday, int Streak);

/// <summary>Thẻ nhập (soạn / nhập nhiều dòng)</summary>
public sealed record FlashCardInput(string? Front, string? Reading, string? Meaning, string? Example);
