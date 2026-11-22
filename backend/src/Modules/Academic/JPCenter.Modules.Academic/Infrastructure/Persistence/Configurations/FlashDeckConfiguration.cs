using JPCenter.Modules.Academic.Domain.Flashcards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Configurations;

/// <summary>
/// academic.flash_decks (database.md 61) + flash_cards (62, bảng con) + card_progress (63, UQ (student_id, card_id)).
/// lesson_id không FK (bài học là bảng con của lộ trình); card_id không FK (thẻ là bảng con) — xóa thẻ thì Application xóa tiến độ (ND-09.E2).
/// </summary>
internal sealed class FlashDeckConfiguration : IEntityTypeConfiguration<FlashDeck>, IEntityTypeConfiguration<CardProgress>
{
    public void Configure(EntityTypeBuilder<FlashDeck> b)
    {
        b.ToTable("flash_decks");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.LessonId);
        b.HasIndex(x => x.Level);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.LessonLabel).HasMaxLength(100);
        b.OwnsMany(x => x.Cards, c =>
        {
            c.ToTable("flash_cards");
            c.WithOwner().HasForeignKey("DeckId");
            c.Property<FlashDeckId>("DeckId");
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            c.Property(x => x.Front).HasMaxLength(FlashCard.MaxFront);
            c.Property(x => x.Reading).HasMaxLength(200);
            c.Property(x => x.Meaning).HasMaxLength(FlashCard.MaxText);
            c.Property(x => x.Example).HasMaxLength(FlashCard.MaxText);
            c.HasIndex("DeckId", nameof(FlashCard.Order));
        });
        b.Navigation(x => x.Cards).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    public void Configure(EntityTypeBuilder<CardProgress> b)
    {
        b.ToTable("card_progress");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.StudentId, x.CardId }).IsUnique();
        b.HasIndex(x => new { x.StudentId, x.Due });
        b.HasOne<FlashDeck>().WithMany().HasForeignKey(x => x.DeckId).OnDelete(DeleteBehavior.Cascade);
    }
}
