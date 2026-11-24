using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Tests.Domain;

/// <summary>HV-17 — hồ sơ du học / XKLĐ</summary>
public sealed class AbroadCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid Staff = Guid.NewGuid();

    private static AbroadCaseInfo Info(string? intake = "2027-04", Money? fee = null) =>
        new(AbroadProgram.Study, " Trường Nhật ngữ ABC ", "Tokyo", " ", intake, JlptLevel.N4, fee ?? Money.FromTrusted(150_000_000), Staff);

    private static AbroadCase New() =>
        AbroadCase.Create(StudentId.New(), new BranchId(Guid.NewGuid()), Info(), ["Hộ chiếu", "Học bạ", " ", "Hộ chiếu"]).Value;

    [Fact]
    public void HV17_tao_ho_so_kiem_thong_tin()
    {
        var c = New();
        (c.Stage, c.Partner, c.Job, c.Documents.Count, c.FeePaid).ShouldBe((AbroadStage.Consult, "Trường Nhật ngữ ABC", (string?)null, 2, Money.Zero));
        var s = StudentId.New();
        var b = new BranchId(Guid.NewGuid());
        AbroadCase.Create(s, b, Info("2027-13"), []).Error.ShouldBe(AbroadErrors.InvalidIntake);
        AbroadCase.Create(s, b, Info("04/2027"), []).Error.ShouldBe(AbroadErrors.InvalidIntake);
        AbroadCase.Create(s, b, Info() with { Partner = " " }, []).Error.ShouldBe(AbroadErrors.PartnerRequired);
        AbroadCase.Create(s, b, Info() with { AssigneeId = Guid.Empty }, []).Error.ShouldBe(AbroadErrors.AssigneeRequired);
        AbroadCase.Create(s, b, Info() with { JlptRequired = JlptLevel.Beginner }, []).Error.ShouldBe(AbroadErrors.InvalidLevel);
    }

    [Fact]
    public void HV17_E2_E3_chuyen_buoc_can_dieu_kien_tung_buoc()
    {
        var c = New();
        c.Advance(Staff, "Vy", Now).IsSuccess.ShouldBeTrue(); // Tư vấn → Giấy tờ
        c.Advance(Staff, "Vy", Now).Error.ShouldBe(AbroadErrors.DocsNotVerified);
        c.SetDocumentState(Guid.NewGuid(), DocumentState.Verified).Error.ShouldBe(AbroadErrors.DocumentNotFound);
        foreach (var d in c.Documents) c.SetDocumentState(d.Id, DocumentState.Verified);
        c.Advance(Staff, "Vy", Now).IsSuccess.ShouldBeTrue(); // → Phỏng vấn

        c.RecordInterview(null, InterviewResult.Pass).Error.ShouldBe(AbroadErrors.InterviewDateRequired);
        c.RecordInterview(new(2026, 12, 1), InterviewResult.Fail);
        c.Advance(Staff, "Vy", Now).Error.ShouldBe(AbroadErrors.InterviewNotPassed); // trượt → ở lại
        c.RecordInterview(new(2026, 12, 15), InterviewResult.Pass);
        c.Advance(Staff, "Vy", Now).IsSuccess.ShouldBeTrue(); // → COE

        c.Advance(Staff, "Vy", Now).Error.ShouldBe(AbroadErrors.CoeMissing);
        c.RecordCoe(null, new(2027, 2, 1)).Error.ShouldBe(AbroadErrors.InvalidDates);
        c.RecordCoe(new(2027, 1, 10), new(2027, 1, 5)).Error.ShouldBe(AbroadErrors.InvalidDates);
        c.RecordCoe(new(2027, 1, 10), new(2027, 2, 20)).IsSuccess.ShouldBeTrue();
        c.Advance(Staff, "Vy", Now).IsSuccess.ShouldBeTrue(); // → Visa

        c.Advance(Staff, "Vy", Now).Error.ShouldBe(AbroadErrors.VisaMissing);
        c.RecordVisa(new(2027, 2, 25), new(2027, 3, 5)).IsSuccess.ShouldBeTrue();
        c.Advance(Staff, "Vy", Now).IsSuccess.ShouldBeTrue();
        c.Stage.ShouldBe(AbroadStage.Departed);
        c.Advance(Staff, "Vy", Now).Error.ShouldBe(AbroadErrors.AlreadyDeparted);
        c.Notes.Count.ShouldBe(5);
        c.Notes[^1].Text.ShouldBe("Chuyển sang bước \"Đã xuất cảnh\".");
    }

    [Fact]
    public void HV17_E1_dung_ho_so_thi_khoa_thao_tac_mo_lai_duoc()
    {
        var c = New();
        c.Cancel(" ", Staff, "Vy", Now).Error.ShouldBe(AbroadErrors.ReasonRequired);
        c.Reopen(Staff, "Vy", Now).Error.ShouldBe(AbroadErrors.NotCancelled);
        c.Cancel("Không chứng minh được tài chính", Staff, "Vy", Now).IsSuccess.ShouldBeTrue();
        c.Advance(Staff, "Vy", Now).Error.ShouldBe(AbroadErrors.Cancelled);
        c.RecordPayment(Money.FromTrusted(1_000_000)).Error.ShouldBe(AbroadErrors.Cancelled);
        c.Update(Info()).Error.ShouldBe(AbroadErrors.Cancelled);
        c.AddNote("Gọi lại sau Tết", Staff, "Vy", Now).IsSuccess.ShouldBeTrue();
        c.Reopen(Staff, "Vy", Now).IsSuccess.ShouldBeTrue();
        (c.IsCancelled, c.CancelReason).ShouldBe((false, (string?)null));
        c.Notes.Select(n => n.Text).ShouldBe(["Dừng hồ sơ: Không chứng minh được tài chính", "Gọi lại sau Tết", "Mở lại hồ sơ."]);
    }

    [Fact]
    public void HV17_dong_phi_khong_vuot_tong()
    {
        var c = New();
        c.RecordPayment(Money.Zero).Error.ShouldBe(AbroadErrors.InvalidPayment);
        c.RecordPayment(Money.FromTrusted(100_000_000)).IsSuccess.ShouldBeTrue();
        c.RecordPayment(Money.FromTrusted(60_000_000)).Error.ShouldBe(AbroadErrors.InvalidPayment);
        c.Update(Info(fee: Money.FromTrusted(90_000_000))).Error.ShouldBe(AbroadErrors.InvalidPayment);
        c.RecordPayment(Money.FromTrusted(50_000_000)).IsSuccess.ShouldBeTrue();
        c.FeePaid.Amount.ShouldBe(150_000_000m);
        c.AddNote(" ", Staff, "Vy", Now).Error.ShouldBe(AbroadErrors.NoteRequired);
    }
}
