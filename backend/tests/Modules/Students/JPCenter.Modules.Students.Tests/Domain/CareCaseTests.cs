using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Tests.Domain;

/// <summary>HV-14 — tính nguy cơ bỏ học, mở / giữ / mở lại case chăm sóc</summary>
public sealed class CareCaseTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;
    private static readonly RiskSignals Clean = new(Taken: 6, Absent: 0, Streak: 0, AttendanceRate: 100, Due: 4, Submitted: 4, AssignmentAvgPct: 85,
        WeakExams: 0, OverdueDebt: false);

    private static RiskAssessment Eval(RiskSignals s) => RiskAssessment.Evaluate(s, RiskThresholds.Default);

    [Fact]
    public void Khong_dau_hieu_thi_khong_co_muc()
    {
        var r = Eval(Clean);
        (r.Level, r.Flagged, r.Score).ShouldBe(((RiskLevel?)null, false, 0));
    }

    [Fact]
    public void Dau_hieu_nhe_cong_1_diem_muc_thap()
    {
        var r = Eval(Clean with { OverdueDebt = true, AttendanceRate = 70 });
        r.Flags.ShouldBe([RiskAssessment.Rate, RiskAssessment.Tuition]);
        (r.Score, r.Level).ShouldBe((2, (RiskLevel?)RiskLevel.Low));
    }

    [Fact]
    public void Chuyen_can_va_nop_bai_chi_xet_khi_du_so_lieu()
    {
        Eval(Clean with { Taken = 3, AttendanceRate = 50 }).Flagged.ShouldBeFalse(); // < 4 buổi
        Eval(Clean with { Due = 1, Submitted = 0 }).Flagged.ShouldBeFalse(); // < 2 bài tới hạn
        Eval(Clean with { Due = 4, Submitted = 1 }).Flags.ShouldBe([RiskAssessment.Submit]); // 25% < 50%
        Eval(Clean with { Due = 4, Submitted = 2 }).Flagged.ShouldBeFalse(); // đúng 50%
    }

    [Fact]
    public void Dau_hieu_nang_3_diem_muc_trung_binh_va_cao()
    {
        Eval(Clean with { Streak = 2 }).Level.ShouldBe(RiskLevel.Medium); // 3 điểm
        Eval(Clean with { Absent = 3, Streak = 2 }).Level.ShouldBe(RiskLevel.High); // 6 điểm
        Eval(Clean with { Streak = 3 }).Level.ShouldBe(RiskLevel.High); // vắng liên tiếp ≥ 3 → cao ngay

        var exams = Eval(Clean with { WeakExams = 2 });
        (exams.Score, exams.Level).ShouldBe((6, (RiskLevel?)RiskLevel.High));
        exams.Flags.ShouldBe([RiskAssessment.Exam]);
    }

    private static CareCase NewCase(RiskAssessment r) => CareCase.Detect(StudentId.New(), Guid.NewGuid(), new BranchId(Guid.NewGuid()), r, At);

    [Fact]
    public void Het_dau_hieu_giu_case_mo()
    {
        var c = NewCase(Eval(Clean with { Streak = 2 }));
        c.Contacted();
        c.Status.ShouldBe(CareStatus.Contacting);

        c.Reassess(Eval(Clean), At.AddDays(1)); // E2
        (c.Status, c.NoLongerFlagged, c.RiskLevel).ShouldBe((CareStatus.Contacting, true, RiskLevel.Medium));
    }

    [Fact]
    public void Case_on_dinh_co_dau_hieu_moi_thi_mo_lai()
    {
        var c = NewCase(Eval(Clean with { Streak = 2 }));
        c.ChangeStatus(CareStatus.Resolved, new DateOnly(2026, 12, 1));
        c.NextAt.ShouldBeNull();

        c.Reassess(Eval(Clean with { Streak = 2 }), At.AddDays(1)); // cùng dấu hiệu cũ → giữ ổn định
        c.Status.ShouldBe(CareStatus.Resolved);

        var later = At.AddDays(2);
        c.Reassess(Eval(Clean with { Streak = 2, OverdueDebt = true }), later); // E3
        (c.Status, c.DetectedAt).ShouldBe((CareStatus.New, later));
        c.Flags.ShouldBe([RiskAssessment.Streak, RiskAssessment.Tuition]);
    }

    [Fact]
    public void Mo_case_bang_tay_chua_co_dau_hieu_lan_tinh_sau_cap_nhat()
    {
        var c = CareCase.OpenManually(StudentId.New(), Guid.NewGuid(), new BranchId(Guid.NewGuid()), At);
        (c.Status, c.RiskLevel, c.Flags.Count, c.DetectedAt).ShouldBe((CareStatus.New, RiskLevel.Low, 0, At));
        c.NoLongerFlagged.ShouldBeTrue();

        c.Reassess(Eval(Clean with { Streak = 2 }), At.AddDays(1));
        c.Flags.ShouldBe([RiskAssessment.Streak]);
        c.NoLongerFlagged.ShouldBeFalse();
    }
}
