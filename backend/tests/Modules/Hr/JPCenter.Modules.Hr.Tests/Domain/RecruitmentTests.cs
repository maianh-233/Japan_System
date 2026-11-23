using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Tests.Domain;

public sealed class RecruitmentTests
{
    private static readonly DateOnly Today = new(2026, 6, 10);
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 3, 0, 0, TimeSpan.Zero);

    private static Result<Job> NewJob(int quantity = 1, DateOnly? deadline = null) =>
        Job.Create("Sale", BranchId.New(), Department.Sales, EmployeeKind.Staff, quantity, null, Today, deadline ?? Today.AddDays(30), [" TopCV ", "TopCV"], null);

    private static Candidate Cand() => Candidate.Apply(JobId.New(), "Lê Hoa", "0901", "Hoa@Mail.vn", "TopCV", Now, null, null).Value;

    [Fact]
    public void TD01_tin_tuyen_kiem_tra_va_chuyen_trang_thai()
    {
        NewJob(quantity: 0).Error.Code.ShouldBe("hr.job.invalid_quantity");
        NewJob(deadline: Today.AddDays(-1)).Error.Code.ShouldBe("hr.job.invalid_deadline");
        var j = NewJob(quantity: 2).Value;
        j.Channels.ShouldBe(["TopCV"]);
        j.Reopen().Error.Code.ShouldBe("hr.job.invalid_status");
        j.Pause().IsSuccess.ShouldBeTrue();
        j.Pause().IsFailure.ShouldBeTrue();
        j.Reopen().IsSuccess.ShouldBeTrue();
        j.CloseIfFilled(1).ShouldBeFalse();
        j.CloseIfFilled(2).ShouldBeTrue();
        j.Status.ShouldBe(JobStatus.Closed);
        j.Reopen().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void TD02_keo_tha_can_thong_tin_rieng()
    {
        Candidate.Apply(JobId.New(), "", "0901", "a@b.vn", "FB", Now, null, null).Error.Code.ShouldBe("hr.candidate.info_required");
        var c = Cand();
        c.Email.ShouldBe("hoa@mail.vn");
        c.MoveTo(CandidateStage.Screening).IsSuccess.ShouldBeTrue();
        c.MoveTo(CandidateStage.Interview).Error.Code.ShouldBe("hr.candidate.stage_action");
        c.MoveTo(CandidateStage.Hired).Error.Code.ShouldBe("hr.candidate.stage_action");
        c.ScheduleInterview(Now.AddDays(2), Guid.NewGuid(), InterviewMode.Online, 11, null).Error.Code.ShouldBe("hr.candidate.invalid_score");
        c.ScheduleInterview(Now.AddDays(2), Guid.NewGuid(), InterviewMode.Online, 8.5m, "Tốt").IsSuccess.ShouldBeTrue();
        c.Stage.ShouldBe(CandidateStage.Interview);
        c.MoveTo(CandidateStage.Screening).IsSuccess.ShouldBeTrue(); // kéo lùi
        c.MoveTo(CandidateStage.Interview).IsSuccess.ShouldBeTrue(); // đã có lịch → kéo lại được
    }

    [Fact]
    public void TD03_TD04_offer_nhan_viec_tu_choi()
    {
        var c = Cand();
        c.Hire().Error.Code.ShouldBe("hr.candidate.no_offer");
        c.SendOffer(Money.FromTrusted(10_000_000), Today.AddDays(-1), Now, Today).Error.Code.ShouldBe("hr.candidate.offer_invalid");
        c.SendOffer(Money.FromTrusted(10_000_000), Today.AddDays(7), Now, Today).IsSuccess.ShouldBeTrue();
        c.Hire().IsSuccess.ShouldBeTrue();
        (c.Stage, c.OfferAccepted).ShouldBe((CandidateStage.Hired, (bool?)true));
        c.Reject("x").Error.Code.ShouldBe("hr.candidate.hired");
        c.MoveTo(CandidateStage.Applied).Error.Code.ShouldBe("hr.candidate.closed");

        var d = Cand();
        d.SendOffer(Money.FromTrusted(9_000_000), Today, Now, Today).IsSuccess.ShouldBeTrue();
        d.DeclineOffer().IsSuccess.ShouldBeTrue();
        (d.Stage, d.RejectReason).ShouldBe((CandidateStage.Rejected, "Từ chối offer"));

        var e = Cand();
        e.Reject(" ").Error.Code.ShouldBe("hr.candidate.reason_required");
        e.Reject("Chưa đạt N2").IsSuccess.ShouldBeTrue();
        e.AddNote(" ", Guid.NewGuid(), "HR", Now).Error.Code.ShouldBe("hr.candidate.note_required");
        e.AddNote("Đã gọi", Guid.NewGuid(), "HR", Now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void OB01_checklist_theo_loai_va_hoan_thanh()
    {
        var staff = Onboarding.Create(null, "Hoa", "0901", "hoa@mail.vn", "Sale", Department.Sales, EmployeeKind.Staff, BranchId.New(), Today, EmployeeId.New()).Value;
        staff.Items.ShouldNotContain(i => i.Key == "demo");
        var teacher = Onboarding.Create(null, "Mai", "0902", "mai@mail.vn", "GV N3", Department.Sales, EmployeeKind.Teacher, BranchId.New(), Today, EmployeeId.New()).Value;
        teacher.Items.ShouldContain(i => i.Key == "demo");
        teacher.Department.ShouldBe(Department.Teaching);

        staff.Tick("khong-co", true, Guid.NewGuid(), "HR", Now).Error.Code.ShouldBe("hr.onboarding.item_not_found");
        foreach (var i in staff.Items.ToList()) staff.Tick(i.Key, true, Guid.NewGuid(), "HR", Now);
        staff.Progress.ShouldBe(100);
        staff.IsComplete.ShouldBeFalse(); // chưa tạo hồ sơ
        staff.LinkEmployee(EmployeeId.New()).IsSuccess.ShouldBeTrue();
        staff.IsComplete.ShouldBeTrue();
        staff.LinkEmployee(EmployeeId.New()).Error.Code.ShouldBe("hr.onboarding.already_created");
        staff.Tick("bank", false, Guid.NewGuid(), "HR", Now);
        staff.Items.Single(i => i.Key == "bank").DoneAt.ShouldBeNull();
    }

    [Fact]
    public void OB01_he_thong_tu_tick_tai_khoan_mot_lan()
    {
        var o = Onboarding.Create(null, "Hoa", "0901", "hoa@mail.vn", "Sale", Department.Sales, EmployeeKind.Staff, BranchId.New(), Today, EmployeeId.New()).Value;
        o.MarkAccountCreated(Now).ShouldBeTrue();
        var item = o.Items.Single(i => i.Key == OnboardingTemplate.AccountKey);
        (item.Done, item.DoneBy, item.DoneByName, item.DoneAt).ShouldBe((true, (Guid?)null, "Hệ thống", (DateTimeOffset?)Now));
        o.MarkAccountCreated(Now.AddHours(1)).ShouldBeFalse(); // đã tick → không đổi
    }
}
