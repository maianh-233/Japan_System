using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Tests.Domain;

/// <summary>HV-15 — chứng nhận hoàn thành khóa</summary>
public sealed class CertificateTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid Staff = Guid.NewGuid();

    private static Enrollment Completed()
    {
        var e = Enrollment.Create(StudentId.New(), Guid.NewGuid(), new BranchId(Guid.NewGuid()), Today.AddMonths(-3));
        e.Complete(Today).IsSuccess.ShouldBeTrue();
        return e;
    }

    [Fact]
    public void HV15_checklist_dieu_kien_giong_frontend()
    {
        var ok = CertificateEligibility.Evaluate(EnrollmentStatus.Completed,
            new CertificateFacts(92, "Thi cuối kỳ", 75, false, Money.Zero), RiskThresholds.Default);
        CertificateEligibility.IsEligible(ok).ShouldBeTrue();
        ok.Select(c => c.Key).ShouldBe(["completed", "attendance", "final_exam", "tuition"]);

        var bad = CertificateEligibility.Evaluate(EnrollmentStatus.Studying,
            new CertificateFacts(70, "Thi cuối kỳ", null, true, Money.FromTrusted(500_000)), RiskThresholds.Default);
        bad.Count(c => !c.Ok).ShouldBe(4);
        bad[1].Label.ShouldBe("Chuyên cần ≥ 80% (hiện 70%)");
        bad[2].Label.ShouldBe("Thi cuối kỳ ≥ 60% (vắng thi)");
        bad[3].Label.ShouldContain("còn nợ");

        var noExam = CertificateEligibility.Evaluate(EnrollmentStatus.Completed, new CertificateFacts(80, null, null, false, Money.Zero),
            RiskThresholds.Default);
        CertificateEligibility.IsEligible(noExam).ShouldBeTrue();
        CertificateEligibility.Evaluate(EnrollmentStatus.Completed, new CertificateFacts(80, "Thi", 59.5m, false, Money.Zero), RiskThresholds.Default)[2]
            .Ok.ShouldBeFalse();
    }

    [Fact]
    public void HV15_ma_chung_nhan_bo_tien_to_lop()
    {
        CertificateCode.Build(2026, "GVV_N3-K12", 7).ShouldBe("CN-2026-N3K12-0007");
        CertificateCode.Build(2026, "gvn_n5-k3", 120).ShouldBe("CN-2026-N5K3-0120");
        CertificateCode.ShortClass("JLPT N2 (tối)").ShouldBe("JLPTN2TI");
    }

    [Fact]
    public void HV15_E1_E2_E3_cap_can_hoan_thanh_ngoai_le_va_cap_1_lan()
    {
        var studying = Enrollment.Create(StudentId.New(), Guid.NewGuid(), new BranchId(Guid.NewGuid()), Today);
        studying.IssueCertificate("CN-1", Now, "a@b.vn", Staff, true, null).Error.ShouldBe(CertificateErrors.NotCompleted);
        studying.IssueCertificate("CN-1", Now, "a@b.vn", Staff, false, "Ngoại lệ").Error.ShouldBe(CertificateErrors.NotCompleted);

        var e = Completed();
        e.IssueCertificate("CN-1", Now, "a@b.vn", Staff, false, " ").Error.ShouldBe(CertificateErrors.NotEligible);
        e.IssueCertificate("CN-1", Now, "khong-phai-email", Staff, true, null).Error.ShouldBe(CertificateErrors.EmailRequired);
        e.IssueCertificate("CN-1", Now, "a@b.vn", Staff, false, new string('x', 501)).Error.ShouldBe(CertificateErrors.ExceptionTooLong);
        e.IssueCertificate("CN-1", Now, " A@B.vn ", Staff, false, " Nằm viện, giám đốc duyệt ").IsSuccess.ShouldBeTrue();
        (e.Certificate!.SentTo, e.Certificate.Exception, e.Certificate.SendCount).ShouldBe(("a@b.vn", "Nằm viện, giám đốc duyệt", 1));
        e.IssueCertificate("CN-2", Now, "a@b.vn", Staff, true, null).Error.ShouldBe(CertificateErrors.AlreadyIssued);

        var eligible = Completed();
        eligible.IssueCertificate("CN-3", Now, "a@b.vn", Staff, true, "không cần").IsSuccess.ShouldBeTrue();
        eligible.Certificate!.Exception.ShouldBeNull();
    }

    [Fact]
    public void HV15_gui_lai_tang_so_lan_gui()
    {
        var e = Completed();
        e.ResendCertificate("a@b.vn", Staff).Error.ShouldBe(CertificateErrors.NotFound);
        e.IssueCertificate("CN-1", Now, "a@b.vn", Staff, true, null);
        e.ResendCertificate("", Staff).Error.ShouldBe(CertificateErrors.EmailRequired);
        e.ResendCertificate("ph@b.vn", Staff).IsSuccess.ShouldBeTrue();
        (e.Certificate!.SentTo, e.Certificate.SendCount).ShouldBe(("ph@b.vn", 2));
    }
}
