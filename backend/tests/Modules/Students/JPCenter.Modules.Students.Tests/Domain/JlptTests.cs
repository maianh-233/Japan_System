using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Tests.Domain;

/// <summary>HV-16 — đăng ký & theo dõi thi JLPT</summary>
public sealed class JlptTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid Staff = Guid.NewGuid();
    private const string Venue = "Hà Nội — ĐH Hà Nội";

    private static Dictionary<JlptLevel, Money> Fees() => new()
    {
        [JlptLevel.N1] = Money.FromTrusted(700_000), [JlptLevel.N2] = Money.FromTrusted(650_000), [JlptLevel.N3] = Money.FromTrusted(600_000),
        [JlptLevel.N4] = Money.FromTrusted(550_000), [JlptLevel.N5] = Money.FromTrusted(550_000),
    };

    private static JlptSession Session() =>
        JlptSession.Create("Tháng 12/2026", new(2026, 12, 6), new(2026, 8, 25), new(2027, 1, 26), Fees(), [Venue, " ", Venue]).Value;

    private static JlptRegistration Register(JlptLevel level = JlptLevel.N3) =>
        JlptRegistration.Create(StudentId.New(), Session(), new BranchId(Guid.NewGuid()), level, Venue).Value;

    private static JlptRegistration Admitted(JlptLevel level = JlptLevel.N3)
    {
        var r = Register(level);
        r.SetDocuments(true, true, true);
        r.MarkPaid(Now);
        r.MarkSubmitted();
        r.SetAdmit("12A3456").IsSuccess.ShouldBeTrue();
        return r;
    }

    [Fact]
    public void HV16_E1_ky_thi_kiem_ngay_le_phi_dia_diem()
    {
        var s = Session();
        (s.Venues.Count, s.Fees.Count, s.FeeFor(JlptLevel.N1).Amount).ShouldBe((1, 5, 700_000m));
        JlptSession.Create("x", new(2026, 12, 6), new(2026, 12, 6), new(2027, 1, 1), Fees(), [Venue]).Error.ShouldBe(JlptErrors.InvalidDates);
        JlptSession.Create("x", new(2026, 12, 6), new(2026, 8, 1), new(2026, 12, 6), Fees(), [Venue]).Error.ShouldBe(JlptErrors.InvalidDates);
        var missing = Fees();
        missing.Remove(JlptLevel.N5);
        JlptSession.Create("x", new(2026, 12, 6), new(2026, 8, 1), new(2027, 1, 1), missing, [Venue]).Error.ShouldBe(JlptErrors.InvalidFees);
        JlptSession.Create("x", new(2026, 12, 6), new(2026, 8, 1), new(2027, 1, 1), Fees(), [" "]).Error.ShouldBe(JlptErrors.VenueRequired);
        JlptSession.Create(" ", new(2026, 12, 6), new(2026, 8, 1), new(2027, 1, 1), Fees(), [Venue]).Error.ShouldBe(JlptErrors.LabelRequired);
        // E6: quá hạn chỉ cảnh báo
        (s.IsClosed(new(2026, 8, 25)), s.IsClosed(new(2026, 8, 26))).ShouldBe((false, true));
    }

    [Fact]
    public void HV16_dang_ky_lay_le_phi_theo_cap_do_va_dia_diem_cua_ky()
    {
        var s = Session();
        var b = new BranchId(Guid.NewGuid());
        JlptRegistration.Create(StudentId.New(), s, b, JlptLevel.Beginner, Venue).Error.ShouldBe(JlptErrors.InvalidLevel);
        JlptRegistration.Create(StudentId.New(), s, b, JlptLevel.N3, "TP.HCM").Error.ShouldBe(JlptErrors.InvalidVenue);
        var r = JlptRegistration.Create(StudentId.New(), s, b, JlptLevel.N3, $" {Venue} ").Value;
        (r.Status, r.Fee.Amount, r.SessionId).ShouldBe((JlptRegistrationStatus.Registered, 600_000m, s.Id));
        r.ChangeExam(s, JlptLevel.N2, Venue).IsSuccess.ShouldBeTrue();
        r.Fee.Amount.ShouldBe(650_000m);
    }

    [Fact]
    public void HV16_E2_E3_tien_do_dung_thu_tu_va_du_giay_to()
    {
        var r = Register();
        r.MarkSubmitted().Error.ShouldBe(JlptErrors.InvalidStatus);
        r.MarkPaid(Now).IsSuccess.ShouldBeTrue();
        r.MarkPaid(Now).Error.ShouldBe(JlptErrors.InvalidStatus);
        r.SetDocuments(true, true, false);
        r.MarkSubmitted().Error.ShouldBe(JlptErrors.DocsMissing);
        r.SetDocuments(true, true, true);
        r.MarkSubmitted().IsSuccess.ShouldBeTrue();
        r.SetDocuments(false, false, false).Error.ShouldBe(JlptErrors.InvalidStatus);
        r.ChangeExam(Session(), JlptLevel.N2, Venue).Error.ShouldBe(JlptErrors.InvalidStatus);
        r.SetAdmit(" ").Error.ShouldBe(JlptErrors.AdmitNoRequired);
        r.SetAdmit(" 12A3456 ").IsSuccess.ShouldBeTrue();
        (r.Status, r.AdmitNo).ShouldBe((JlptRegistrationStatus.Admit, "12A3456"));
    }

    [Fact]
    public void HV16_cham_diem_theo_quy_che_diem_liet_va_tong()
    {
        JlptScoring.Judge(JlptLevel.N3, [40, 30, 30])!.ShouldSatisfyAllConditions(x => x.Total.ShouldBe(100), x => x.Passed.ShouldBeTrue());
        // Đủ tổng nhưng liệt 1 phần
        var liet = JlptScoring.Judge(JlptLevel.N3, [60, 60, 18])!;
        (liet.Passed, liet.FailSections.Single()).ShouldBe((false, "Nghe hiểu"));
        // Không liệt nhưng thiếu tổng
        JlptScoring.Judge(JlptLevel.N1, [33, 33, 33])!.Passed.ShouldBeFalse();
        JlptScoring.Judge(JlptLevel.N5, [60, 20])!.Passed.ShouldBeTrue();
        JlptScoring.Judge(JlptLevel.N5, [60, 20, 0]).ShouldBeNull();
        JlptScoring.Judge(JlptLevel.N4, [121, 20]).ShouldBeNull();
        JlptScoring.Judge(JlptLevel.Beginner, [1]).ShouldBeNull();
    }

    [Fact]
    public void HV16_E4_E5_ket_qua_va_chung_chi_goc()
    {
        var r = Admitted();
        r.RecordResult([10, 10]).Error.ShouldBe(JlptErrors.InvalidScores);
        r.RecordResult([20, 20, 20]).IsSuccess.ShouldBeTrue();
        (r.Status, r.Passed).ShouldBe((JlptRegistrationStatus.Done, false));
        r.ReceiveCertificate("N3-001", Now, Staff).Error.ShouldBe(JlptErrors.NotPassed);
        r.Cancel("x").Error.ShouldBe(JlptErrors.InvalidStatus);

        var p = Admitted();
        p.HandCertificate(Now, Staff, "Mẹ").Error.ShouldBe(JlptErrors.CertNotReceived);
        p.RecordResult([45, 40, 35]).IsSuccess.ShouldBeTrue();
        p.ReceiveCertificate(" ", Now, Staff).Error.ShouldBe(JlptErrors.CertNoRequired);
        p.ReceiveCertificate("N3-001", Now, Staff).IsSuccess.ShouldBeTrue();
        p.HandCertificate(Now, Staff, " ").Error.ShouldBe(JlptErrors.HandedToRequired);
        p.HandCertificate(Now, Staff, "Phụ huynh — Nguyễn Văn Bình").IsSuccess.ShouldBeTrue();
        p.HandCertificate(Now, Staff, "x").Error.ShouldBe(JlptErrors.CertAlreadyHanded);
        p.ReceiveCertificate("N3-002", Now, Staff).Error.ShouldBe(JlptErrors.CertAlreadyHanded);
        p.Certificate!.No.ShouldBe("N3-001");
    }

    [Fact]
    public void HV16_vang_thi_va_huy()
    {
        var a = Admitted();
        a.MarkAbsent().IsSuccess.ShouldBeTrue();
        a.Cancel(null).Error.ShouldBe(JlptErrors.InvalidStatus);

        var c = Register();
        c.Cancel(" Đổi kỳ thi ").IsSuccess.ShouldBeTrue();
        (c.Status, c.Note).ShouldBe((JlptRegistrationStatus.Cancelled, "Đổi kỳ thi"));
        c.Cancel(null).Error.ShouldBe(JlptErrors.InvalidStatus);
        c.MarkPaid(Now).Error.ShouldBe(JlptErrors.InvalidStatus);
    }
}
