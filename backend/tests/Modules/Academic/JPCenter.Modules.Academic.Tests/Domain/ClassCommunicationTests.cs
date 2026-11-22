using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Flashcards;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

/// <summary>ND-07 → ND-09 — thông báo lớp, đánh giá GV, flashcard</summary>
public sealed class ClassCommunicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly ClassId Class = new(Guid.NewGuid());
    private static readonly Guid Teacher = Guid.NewGuid(), Student = Guid.NewGuid();

    private static Result<ClassAnnouncement> Announce(DateTimeOffset? at = null, string title = "Nghỉ lễ") =>
        ClassAnnouncement.Create(Class, title, "Lớp nghỉ ngày 1/1", [AnnouncementAudience.Student, AnnouncementAudience.Student],
            [AnnouncementChannel.Portal], false, [], Teacher, at, Now);

    [Fact]
    public void Thong_bao_hen_gio_huy_khi_con_cho_dem_xem_moi_nguoi_1_lan()
    {
        Announce(title: " ").Error.ShouldBe(AnnouncementErrors.ContentRequired);
        Announce(Now.AddMinutes(-1)).Error.ShouldBe(AnnouncementErrors.InvalidSchedule);
        ClassAnnouncement.Create(Class, "x", "y", [], [AnnouncementChannel.Portal], false, [], Teacher, null, Now).Error
            .ShouldBe(AnnouncementErrors.TargetRequired);

        var scheduled = Announce(Now.AddHours(2)).Value;
        (scheduled.IsPending, scheduled.Audience.Count).ShouldBe((true, 1));
        scheduled.Cancel(Now).IsSuccess.ShouldBeTrue();
        scheduled.MarkSent(10, Now).Error.ShouldBe(AnnouncementErrors.NotPending);

        var a = Announce().Value;
        a.MarkRead(Student).ShouldBeFalse(); // chưa gửi
        a.MarkSent(0, Now).Error.ShouldBe(AnnouncementErrors.NoRecipients);
        a.MarkSent(25, Now).IsSuccess.ShouldBeTrue();
        a.Cancel(Now).Error.ShouldBe(AnnouncementErrors.NotPending);
        a.MarkRead(Student).ShouldBeTrue();
        a.MarkRead(Student).ShouldBeFalse();
        (a.ReadCount, a.Recipients).ShouldBe((1, 25));
    }

    [Fact]
    public void Danh_gia_GV_kiem_sao_va_thoi_gian_khao_sat()
    {
        TeacherReview.Create(Class, Teacher, Student, ReviewPeriod.Mid, 6, 5, 5, 5, 5, null, false, Now).Error.ShouldBe(TeacherReviewErrors.InvalidRating);
        TeacherReview.Create(Class, Teacher, Student, ReviewPeriod.Mid, 5, 5, 5, 5, 5, new string('a', 2001), false, Now).Error
            .ShouldBe(TeacherReviewErrors.CommentTooLong);
        TeacherReview.Create(Class, Teacher, Student, ReviewPeriod.Final, 4, 4, 5, 3, 5, "  ", true, Now).Value.Comment.ShouldBeNull();

        var mid = new DateOnly(2026, 10, 1);
        var end = new DateOnly(2026, 12, 31);
        ReviewSurvey.OpenPeriod(new DateOnly(2026, 9, 30), mid, end).ShouldBeNull();
        ReviewSurvey.OpenPeriod(mid, mid, end).ShouldBe(ReviewPeriod.Mid);
        ReviewSurvey.OpenPeriod(new DateOnly(2026, 12, 16), mid, end).ShouldBe(ReviewPeriod.Mid);
        ReviewSurvey.OpenPeriod(new DateOnly(2026, 12, 17), mid, end).ShouldBe(ReviewPeriod.Final);
        ReviewSurvey.OpenPeriod(new DateOnly(2027, 1, 7), mid, end).ShouldBe(ReviewPeriod.Final);
        ReviewSurvey.OpenPeriod(new DateOnly(2027, 1, 8), mid, end).ShouldBeNull();
        ReviewSurvey.OpenPeriod(new DateOnly(2026, 11, 1), null, end).ShouldBeNull();
    }

    [Fact]
    public void Bo_the_them_sua_xoa_sap_xep_va_on_Leitner()
    {
        FlashDeck.Create(" ", JlptLevel.N5, DeckKind.Vocab, null, null).Error.ShouldBe(FlashcardErrors.TitleRequired);
        var deck = FlashDeck.Create("Từ vựng bài 5", JlptLevel.N5, DeckKind.Vocab, new LessonId(Guid.NewGuid()), "Bài 5").Value;
        deck.AddCard("食べる", "たべる", " ", null).Error.ShouldBe(FlashcardErrors.CardRequired);
        var a = deck.AddCard(" 食べる ", "たべる", "ăn", null).Value;
        var b = deck.AddCard("飲む", "のむ", "uống", "水を飲む").Value;
        var c = deck.AddCard("見る", null, "xem", null).Value;
        deck.UpdateCard(b.Id, "飲む", "のむ", "uống (nước)", null).IsSuccess.ShouldBeTrue();
        deck.Reorder([c.Id, a.Id]).Error.ShouldBe(FlashcardErrors.InvalidOrder);
        deck.Reorder([c.Id, a.Id, b.Id]).IsSuccess.ShouldBeTrue();
        deck.Cards.Select(x => (x.Front, x.Order)).ShouldBe([("見る", 1), ("食べる", 2), ("飲む", 3)]);
        deck.RemoveCard(a.Id).IsSuccess.ShouldBeTrue();
        deck.Cards.Select(x => x.Order).ShouldBe([1, 2]);
        deck.RemoveCard(a.Id).Error.ShouldBe(FlashcardErrors.CardNotFound);

        var today = new DateOnly(2026, 11, 20);
        var p = CardProgress.Start(Student, deck.Id, b.Id, today);
        p.Review(true, today, Now);
        p.Review(true, today, Now);
        (p.Box, p.Due).ShouldBe((2, today.AddDays(3)));
        p.Review(false, today, Now);
        (p.Box, p.Due, p.Lapses, p.Reviews).ShouldBe((0, today, 1, 3));
    }
}
