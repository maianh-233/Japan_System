using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

/// <summary>GĐ 15 — lên lớp (DT-17), nhận xét cuối khóa (DT-19)</summary>
public sealed class PromotionEvaluationTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;
    private static readonly ClassId From = ClassId.New(), To = ClassId.New();

    private static PromotionCandidate New() =>
        PromotionCandidate.Create(Guid.NewGuid(), new BranchId(Guid.NewGuid()), From, PromotionSource.Teacher).Value;

    [Fact]
    public void Giu_cho_can_lop_dich_va_han_hop_le()
    {
        var p = New();
        p.HoldSeat(null, Today.AddDays(10), Today).Error.Code.ShouldBe("academic.promotion.to_class_required"); // E1
        p.HoldSeat(From, Today.AddDays(10), Today).Error.Code.ShouldBe("academic.promotion.same_class");
        p.HoldSeat(To, Today.AddDays(-1), Today).Error.Code.ShouldBe("academic.promotion.invalid_hold_until");

        p.ChooseTarget(To).IsSuccess.ShouldBeTrue();
        p.HoldSeat(null, Today.AddDays(10), Today).IsSuccess.ShouldBeTrue(); // dùng lớp đã chọn
        (p.SeatHeld, p.ToClassId, p.HoldUntil).ShouldBe((true, (ClassId?)To, (DateOnly?)Today.AddDays(10)));

        p.ChooseTarget(null).IsSuccess.ShouldBeTrue();
        p.SeatHeld.ShouldBeFalse();
    }

    [Fact]
    public void Han_giu_cho_mac_dinh_7_ngay_truoc_khai_giang()
    {
        PromotionCandidate.DefaultHoldUntil(Today.AddDays(30), Today).ShouldBe(Today.AddDays(23));
        PromotionCandidate.DefaultHoldUntil(Today.AddDays(3), Today).ShouldBe(Today);
    }

    [Fact]
    public void Job_tra_cho_khi_qua_han()
    {
        var p = New();
        p.HoldSeat(To, Today, Today).IsSuccess.ShouldBeTrue();
        p.ExpireHold(Today).ShouldBeFalse(); // còn trong hạn
        p.ExpireHold(Today.AddDays(1)).ShouldBeTrue(); // E3
        (p.SeatHeld, p.HoldUntil, p.ToClassId).ShouldBe((false, (DateOnly?)null, (ClassId?)To));
    }

    [Fact]
    public void Giao_Sale_quan_tam_tu_van_chot()
    {
        var p = New();
        p.MarkInterest(InterestBy.Parent, At).Error.Code.ShouldBe("academic.promotion.to_class_required");
        p.HoldSeat(To, Today.AddDays(5), Today);

        var sale = Guid.NewGuid();
        p.HandOver(sale, At).IsSuccess.ShouldBeTrue();
        p.HandOver(null, At.AddDays(1)).IsSuccess.ShouldBeTrue(); // lớp kết thúc sau "giao ngay" — giữ Sale, giữ mốc giao
        (p.SaleId, p.HandedOverAt).ShouldBe(((Guid?)sale, (DateTimeOffset?)At));

        p.MarkInterest(InterestBy.Parent, At).IsSuccess.ShouldBeTrue();
        p.MarkInterest(InterestBy.Student, At.AddHours(1)).IsSuccess.ShouldBeTrue();
        p.InterestBy.ShouldBe(InterestBy.Parent); // giữ lần đầu

        p.AddNote(" ", "call", sale, "Sale", At).Error.Code.ShouldBe("academic.promotion.note_required");
        p.AddNote("PH đồng ý học tiếp", "call", sale, "Sale", At).IsSuccess.ShouldBeTrue();
        p.Status.ShouldBe(PromotionStatus.Consulting);

        var enrollment = Guid.NewGuid();
        p.Confirm(enrollment, sale, At).IsSuccess.ShouldBeTrue();
        (p.Status, p.SeatHeld, p.EnrollmentId).ShouldBe((PromotionStatus.Confirmed, false, (Guid?)enrollment));
        p.Decline("x").Error.Code.ShouldBe("academic.promotion.closed");
        p.ExpireHold(Today.AddDays(30)).ShouldBeFalse();
    }

    [Fact]
    public void Tu_choi_can_ly_do()
    {
        var p = New();
        p.Decline(" ").Error.Code.ShouldBe("academic.promotion.decline_reason_required");
        p.Decline("Chuyển nơi ở").IsSuccess.ShouldBeTrue();
        p.Status.ShouldBe(PromotionStatus.Declined);
    }

    private static readonly EvaluationScores Good = new(4, 4, 3, 5, 4, 5);

    [Fact]
    public void Nhan_xet_cham_1_den_5_sao()
    {
        StudentEvaluation.Create(From, Guid.NewGuid(), Guid.NewGuid(), Good with { Speaking = 6 }, null, null, EvalRecommendation.NextLevel, null, null, Today)
            .Error.Code.ShouldBe("academic.evaluation.invalid_rating"); // E1
        StudentEvaluation.Create(From, Guid.NewGuid(), Guid.NewGuid(), Good with { Attitude = 0 }, null, null, EvalRecommendation.NextLevel, null, null, Today)
            .IsFailure.ShouldBeTrue();

        var e = StudentEvaluation.Create(From, Guid.NewGuid(), Guid.NewGuid(), Good, " Nghe tốt ", "", EvalRecommendation.Jlpt, null, null, Today).Value;
        (e.Listening, e.Strengths, e.Weaknesses, e.Recommendation).ShouldBe((5, "Nghe tốt", (string?)null, EvalRecommendation.Jlpt));
        Good.SkillAverage.ShouldBe(4.0m);
    }

    [Fact]
    public void Nhan_xet_sua_toi_14_ngay_sau_khi_lop_ket_thuc()
    {
        var gv = Guid.NewGuid();
        var end = Today.AddDays(-14);
        var e = StudentEvaluation.Create(From, Guid.NewGuid(), gv, Good, null, null, EvalRecommendation.NextLevel, null, end, Today).Value;
        e.Update(gv, Good with { Vocab = 2 }, null, null, EvalRecommendation.Repeat, null, end, Today).IsSuccess.ShouldBeTrue();
        e.Update(gv, Good, null, null, EvalRecommendation.NextLevel, null, end, Today.AddDays(1)).Error.Code.ShouldBe("academic.evaluation.locked");
        (e.Vocab, e.Recommendation).ShouldBe((2, EvalRecommendation.Repeat));
    }
}
