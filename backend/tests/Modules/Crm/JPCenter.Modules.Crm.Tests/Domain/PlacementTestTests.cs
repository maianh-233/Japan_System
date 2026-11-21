using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Domain.PlacementTests;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Tests.Domain;

public sealed class PlacementTestTests
{
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 11, 21, 2, 0, 0, TimeSpan.Zero);
    private static readonly Guid[] Vocab = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
    private static readonly Guid[] Grammar = [Guid.NewGuid(), Guid.NewGuid()];
    private const string Token = "abcdefghijklmnopqrstuvwxyz012345";

    private static readonly PlacementSection[] Sections =
        [new(JlptLevel.N5, JapaneseSkill.Vocab, 2), new(JlptLevel.N5, JapaneseSkill.Grammar, 2)];

    private static readonly PlacementBand[] Bands =
        [new(60, "N4", "N4 tối 2-4-6"), new(0, "Sơ cấp", "N5 cấp tốc"), new(85, "N3", "")];

    private static IReadOnlyList<Guid> Pool(JlptLevel level, JapaneseSkill skill) =>
        level != JlptLevel.N5 ? [] : skill == JapaneseSkill.Vocab ? Vocab : skill == JapaneseSkill.Grammar ? Grammar : [];

    private static PlacementTest Test(IReadOnlyList<PlacementBand>? bands = null) =>
        PlacementTest.Create("PT01", "Test đầu vào", null, Sections, 30, bands ?? Bands, [.. Vocab[..2], .. Grammar], Author).Value;

    private static PlacementTest Active()
    {
        var t = Test();
        t.Activate().IsSuccess.ShouldBeTrue();
        return t;
    }

    private static PlacementAttempt Attempt(PlacementTest? test = null) =>
        PlacementAttempt.Send(test ?? Active(), LeadId.New(), Token, ContactChannel.Zalo, Author, At, 3).Value;

    [Fact]
    public void CRM_12_boc_cau_theo_thu_tu_va_E2_thieu_cau_liet_ke_tung_phan()
    {
        PlacementDraw.Draw(Sections, Pool).Value.ShouldBe([Vocab[0], Vocab[1], Grammar[0], Grammar[1]]);

        var shuffled = PlacementDraw.Draw(Sections, Pool, new Random(7)).Value;
        shuffled.Count.ShouldBe(4);
        shuffled.Distinct().Count().ShouldBe(4);

        var err = PlacementDraw.Draw([new(JlptLevel.N5, JapaneseSkill.Vocab, 10), new(JlptLevel.N4, JapaneseSkill.Kanji, 1)], Pool).Error;
        err.Code.ShouldBe("crm.placement_test.no_question");
        err.Message.ShouldContain("N5 · Từ vựng: cần 10, chỉ có 3 câu đã duyệt");
        err.Message.ShouldContain("N4 · Chữ Hán: cần 1, chỉ có 0 câu đã duyệt");
    }

    [Fact]
    public void CRM_12_E1_E3_E4_kiem_tra_bai_test()
    {
        Guid[] q = [Vocab[0]];
        PlacementTest.Create("x", "T", null, [], 30, Bands, q, Author).Error.ShouldBe(PlacementTestErrors.NoSection);
        PlacementTest.Create("x", "T", null, Sections, 0, Bands, q, Author).Error.ShouldBe(PlacementTestErrors.InvalidDuration);
        PlacementTest.Create("x", "T", null, Sections, 181, Bands, q, Author).Error.ShouldBe(PlacementTestErrors.InvalidDuration);
        PlacementTest.Create("x", "T", null, Sections, 30, [new(10, "N5", "")], q, Author).Error.ShouldBe(PlacementTestErrors.BandZeroRequired);
        PlacementTest.Create("x", "T", null, Sections, 30, [new(0, "A", ""), new(0, "B", "")], q, Author).Error
            .ShouldBe(PlacementTestErrors.InvalidBand);
        PlacementTest.Create("x", " ", null, Sections, 30, Bands, q, Author).Error.ShouldBe(PlacementTestErrors.NameRequired);
        PlacementTest.Create("x", "T", null, [new(JlptLevel.N5, JapaneseSkill.Vocab, 0)], 30, Bands, q, Author).Error
            .ShouldBe(PlacementTestErrors.InvalidSection);
        PlacementTest.Create("x", "T", null, Sections, 30, Bands, [], Author).Error.ShouldBe(PlacementTestErrors.NoQuestion);
    }

    [Fact]
    public void Thang_xep_tang_dan_goi_y_moc_cao_nhat_dat_toi()
    {
        var t = Test();
        t.Bands.Select(b => b.MinPct).ShouldBe([0m, 60m, 85m]);
        t.Suggest(59.9m)!.Level.ShouldBe("Sơ cấp");
        t.Suggest(60)!.Level.ShouldBe("N4");
        t.Suggest(100)!.Level.ShouldBe("N3");
    }

    [Fact]
    public void CRM_12_E6_da_co_nguoi_lam_chi_sua_ten_mo_ta_thang()
    {
        var t = Test();
        t.UpdateInfo("Test mới", "Mô tả", [new(0, "N5", "N5")]).IsSuccess.ShouldBeTrue();
        t.Restructure(Sections, 45, [Vocab[2]], hasAttempts: true).Error.ShouldBe(PlacementTestErrors.HasAttempts);
        t.Restructure(Sections, 45, [Vocab[2], Vocab[2]], hasAttempts: false).IsSuccess.ShouldBeTrue();
        (t.DurationMinutes, t.QuestionIds.Count).ShouldBe((45, 1));
    }

    [Fact]
    public void Trang_thai_bai_test()
    {
        var t = Test();
        t.Status.ShouldBe(PlacementTestStatus.Draft);
        t.Activate().IsSuccess.ShouldBeTrue();
        t.Archive();
        t.Status.ShouldBe(PlacementTestStatus.Archived);
        t.Activate().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Cham_diem_theo_ky_nang_bo_trong_tinh_sai_cau_mat_bi_bo()
    {
        var keys = new Dictionary<Guid, AnswerKey>
        {
            [Vocab[0]] = new(JapaneseSkill.Vocab, 1), [Vocab[1]] = new(JapaneseSkill.Vocab, 2), [Grammar[0]] = new(JapaneseSkill.Grammar, 0),
        };
        var answers = new Dictionary<Guid, int> { [Vocab[0]] = 1, [Vocab[1]] = -1, [Grammar[0]] = 0 };
        var s = PlacementScore.Grade([Vocab[0], Vocab[1], Grammar[0], Grammar[1]], keys, answers);

        (s.Correct, s.Total, s.Percent).ShouldBe((2, 3, 66.67m));
        s.BySkill[JapaneseSkill.Vocab].ShouldBe(new SkillScore(1, 2));
        s.BySkill[JapaneseSkill.Grammar].ShouldBe(new SkillScore(1, 1));
        PlacementScore.Grade([], keys, answers).Percent.ShouldBe(0);
    }

    [Fact]
    public void CRM_13_gui_link_chi_bai_dang_dung_hieu_luc_1_den_30_ngay()
    {
        PlacementAttempt.Send(Test(), LeadId.New(), Token, ContactChannel.Zalo, Author, At, 3).Error.ShouldBe(PlacementTestErrors.NotActive);
        PlacementAttempt.Send(Active(), LeadId.New(), Token, ContactChannel.Zalo, Author, At, 0).Error
            .ShouldBe(PlacementAttemptErrors.InvalidValidity);
        Should.Throw<ArgumentException>(() => PlacementAttempt.Send(Active(), LeadId.New(), "ngan", ContactChannel.Sms, Author, At, 3));
        var a = Attempt();
        (a.Status, a.ExpiresAt).ShouldBe((AttemptStatus.Sent, At.AddDays(3)));
    }

    [Fact]
    public void CRM_13_lam_bai_nop_cham_va_ghi_ket_qua()
    {
        var test = Active();
        var a = Attempt(test);
        a.Start(At.AddHours(1)).IsSuccess.ShouldBeTrue();
        a.Start(At.AddHours(1)).Error.ShouldBe(PlacementAttemptErrors.Started);
        a.RemainingSeconds(30, At.AddHours(1).AddMinutes(10)).ShouldBe(20 * 60);
        a.RemainingSeconds(30, At.AddHours(2)).ShouldBe(0);
        a.MarkLogged().Error.ShouldBe(PlacementAttemptErrors.NotGraded);

        var score = new PlacementScore(3, 4, new() { [JapaneseSkill.Vocab] = new(2, 2), [JapaneseSkill.Grammar] = new(1, 2) });
        a.Submit(new Dictionary<Guid, int> { [Vocab[0]] = 1 }, score, test.Suggest(score.Percent), At.AddHours(1.5)).IsSuccess.ShouldBeTrue();
        (a.Status, a.Correct, a.Total, a.SuggestedLevel, a.SuggestedCourse).ShouldBe((AttemptStatus.Done, 3, 4, "N4", "N4 tối 2-4-6"));
        a.Submit(new Dictionary<Guid, int>(), score, null, At).Error.ShouldBe(PlacementAttemptErrors.NotInProgress);

        a.MarkLogged().IsSuccess.ShouldBeTrue();
        a.MarkLogged().Error.ShouldBe(PlacementAttemptErrors.AlreadyLogged);
    }

    [Fact]
    public void CRM_13_E1_mo_link_sau_han()
    {
        var a = Attempt();
        a.Start(At.AddDays(4)).Error.ShouldBe(PlacementAttemptErrors.Expired);
        a.Status.ShouldBe(AttemptStatus.Expired);
        a.Start(At).Error.ShouldBe(PlacementAttemptErrors.Expired);
    }

    [Fact]
    public void CRM_13_E4_nop_khi_chua_bat_dau()
    {
        var a = Attempt();
        a.Submit(new Dictionary<Guid, int>(), new PlacementScore(0, 0, []), null, At).Error.ShouldBe(PlacementAttemptErrors.NotInProgress);
        a.SaveAnswers(new Dictionary<Guid, int>()).Error.ShouldBe(PlacementAttemptErrors.NotInProgress);
    }

    [Fact]
    public void CRM_13_E7_job_het_han_cham_phan_da_luu()
    {
        var sent = Attempt();
        var grade = (IReadOnlyDictionary<Guid, int> _) => (new PlacementScore(1, 4, []), (PlacementBand?)new(0, "Sơ cấp", "N5"));
        sent.ExpireIfDue(At.AddDays(1), grade).ShouldBeFalse();
        sent.ExpireIfDue(At.AddDays(4), grade).ShouldBeTrue();
        (sent.Status, sent.Total).ShouldBe((AttemptStatus.Expired, (int?)null));

        var doing = Attempt();
        doing.Start(At.AddDays(2));
        doing.SaveAnswers(new Dictionary<Guid, int> { [Vocab[0]] = 1 }).IsSuccess.ShouldBeTrue();
        IReadOnlyDictionary<Guid, int>? graded = null;
        doing.ExpireIfDue(At.AddDays(4), ans => { graded = ans; return grade(ans); }).ShouldBeTrue();
        graded!.Keys.ShouldBe([Vocab[0]]);
        (doing.Status, doing.Correct, doing.SuggestedLevel, doing.IsGraded).ShouldBe((AttemptStatus.Expired, 1, "Sơ cấp", true));
        doing.MarkLogged().IsSuccess.ShouldBeTrue();
        doing.ExpireIfDue(At.AddDays(5), grade).ShouldBeFalse();
    }
}
