using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

/// <summary>GĐ 15 — đơn hủy buổi & mời dạy thay (DT-16), đơn xin nghỉ & học bù (DT-18)</summary>
public sealed class RequestTests
{
    private static readonly DateOnly Day = new(2026, 12, 7);
    private static readonly DateTime Start = Day.ToDateTime(new TimeOnly(18, 0));
    private static readonly DateTime Morning = Day.ToDateTime(new TimeOnly(9, 0));
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;
    private static readonly Guid Gv = Guid.NewGuid(), Gv2 = Guid.NewGuid(), Staff = Guid.NewGuid(), Hv = Guid.NewGuid();
    private static readonly BranchId Branch = new(Guid.NewGuid());

    private static Result<SessionCancelRequest> Cancel(string reason, DateTime now) =>
        SessionCancelRequest.Create(ClassSessionId.New(), ClassId.New(), Branch, Gv, "sick", reason, null, Start, now, At);

    private static Result<LeaveRequest> Leave(string reason, DateTime now, ClassSessionId? makeup = null) =>
        LeaveRequest.Create(Hv, ClassId.New(), ClassSessionId.New(), Branch, "sick", reason, RequestedBy.Parent, Guid.NewGuid(), "PH", makeup,
            Start, now, At);

    [Fact]
    public void Don_huy_buoi_chan_buoi_da_bat_dau_va_ly_do_ngan()
    {
        Cancel("Ốm sốt cao không dạy được", Start).Error.Code.ShouldBe("academic.session.already_started"); // E1
        Cancel("Ốm", Morning).Error.Code.ShouldBe("academic.cancel_request.reason_required"); // E2: < 10 ký tự
        var r = Cancel("  Ốm sốt cao không dạy được  ", Morning).Value;
        (r.Status, r.Reason).ShouldBe((RequestStatus.Pending, "Ốm sốt cao không dạy được"));
    }

    [Fact]
    public void Don_huy_buoi_thu_hoi_duyet_tu_choi_chi_khi_cho_duyet()
    {
        var r = Cancel("Ốm sốt cao không dạy được", Morning).Value;
        r.Reject(Staff, At, " ").Error.Code.ShouldBe("academic.request.reject_reason_required");
        r.Approve(Staff, At, "Mời GV khác dạy thay").IsSuccess.ShouldBeTrue();
        r.Withdraw(At).Error.Code.ShouldBe("academic.request.not_pending");
        r.Reject(Staff, At, "x").Error.Code.ShouldBe("academic.request.not_pending"); // E3

        var w = Cancel("Ốm sốt cao không dạy được", Morning).Value;
        w.Withdraw(At).IsSuccess.ShouldBeTrue();
        w.Status.ShouldBe(RequestStatus.Withdrawn);

        var o = Cancel("Ốm sốt cao không dạy được", Morning).Value;
        o.CloseOrphan(At); // E6
        (o.Status, o.Note).ShouldBe((RequestStatus.Rejected, "Buổi học hoặc lớp học liên kết không còn tồn tại"));
    }

    private static SubstituteInvite Invite(Guid to, DateTimeOffset respondBy) =>
        SubstituteInvite.Create(ClassSessionId.New(), ClassId.New(), Branch, null, Gv, to, "GV ốm", Staff, At, respondBy).Value;

    [Fact]
    public void Loi_moi_day_thay_nhan_tu_choi_het_han()
    {
        SubstituteInvite.Create(ClassSessionId.New(), ClassId.New(), Branch, null, Gv, Gv, "x", Staff, At, At.AddHours(2))
            .Error.Code.ShouldBe("academic.invite.invalid");
        SubstituteInvite.Create(ClassSessionId.New(), ClassId.New(), Branch, null, Gv, Gv2, "x", Staff, At, At)
            .Error.Code.ShouldBe("academic.invite.invalid");

        var a = Invite(Gv2, At.AddHours(2));
        a.Accept(At.AddHours(1)).IsSuccess.ShouldBeTrue();
        a.Decline("bận", At.AddHours(1)).Error.Code.ShouldBe("academic.invite.closed"); // E4

        var late = Invite(Gv2, At.AddHours(2));
        late.Accept(At.AddHours(3)).Error.Code.ShouldBe("academic.invite.closed");
        late.Status.ShouldBe(InviteStatus.Expired);

        var other = Invite(Guid.NewGuid(), At.AddHours(2));
        other.Expire("GV khác đã nhận", At);
        (other.Status, other.DeclineReason).ShouldBe((InviteStatus.Expired, "GV khác đã nhận"));
        other.Accept(At).Error.Code.ShouldBe("academic.invite.closed"); // E5: người sau

        var job = Invite(Guid.NewGuid(), At.AddHours(2));
        job.ExpireIfOverdue(At.AddHours(1)).ShouldBeFalse();
        job.ExpireIfOverdue(At.AddHours(3)).ShouldBeTrue();
        job.DeclineReason.ShouldBe("Quá hạn trả lời");
    }

    [Fact]
    public void Don_xin_nghi_chan_qua_muon_ly_do_ngan_va_thu_hoi()
    {
        Leave("Ốm sốt", Start.AddMinutes(1)).Error.Code.ShouldBe("academic.leave_request.too_late"); // E1
        Leave("Ốm", Morning).Error.Code.ShouldBe("academic.leave_request.reason_required");
        var r = Leave("Ốm sốt", Morning).Value;
        r.By.ShouldBe(RequestedBy.Parent);
        r.Withdraw(At).IsSuccess.ShouldBeTrue();
        r.Approve(Staff, At, null).Error.Code.ShouldBe("academic.request.not_pending"); // E4
    }

    [Fact]
    public void Don_nghi_bo_dang_ky_bu_khi_day_va_chi_ghi_ket_qua_bu_khi_da_duyet()
    {
        var r = Leave("Ốm sốt", Morning, ClassSessionId.New()).Value;
        r.RecordMakeupResult(AttendanceStatus.Present);
        r.MakeupResult.ShouldBeNull(); // chưa duyệt

        r.Approve(Staff, At, null).IsSuccess.ShouldBeTrue();
        r.RecordMakeupResult(AttendanceStatus.Late);
        r.MakeupResult.ShouldBe(AttendanceStatus.Late);

        var full = Leave("Ốm sốt", Morning, ClassSessionId.New()).Value;
        full.DropMakeup(); // E3
        full.MakeupSessionId.ShouldBeNull();
    }

    private static ClassSession Session(ClassId cls, DateOnly date, LessonId? lesson) =>
        ClassSession.Create(cls, Branch, 5, date, new TimeOnly(18, 0), new TimeOnly(20, 0), SessionKind.Lesson, lesson, null,
            SessionOrigin.Regular, null, Guid.NewGuid().ToString("N")).Value;

    [Fact]
    public void Buoi_hoc_bu_lop_khac_cung_khoa_cung_bai_trong_3_tuan()
    {
        var lesson = LessonId.New();
        var original = Session(ClassId.New(), Day, lesson);
        var now = Morning;

        MakeupRules.Check(original, Session(ClassId.New(), Day.AddDays(3), lesson), true, now).IsSuccess.ShouldBeTrue();
        MakeupRules.Check(original, Session(ClassId.New(), Day.AddDays(21), lesson), true, now).IsSuccess.ShouldBeTrue();
        MakeupRules.Check(original, Session(ClassId.New(), Day.AddDays(22), lesson), true, now).Error.Code.ShouldBe("academic.leave_request.makeup_invalid");
        MakeupRules.Check(original, Session(ClassId.New(), Day.AddDays(3), lesson), false, now).IsFailure.ShouldBeTrue(); // khác khóa
        MakeupRules.Check(original, Session(ClassId.New(), Day.AddDays(3), LessonId.New()), true, now).IsFailure.ShouldBeTrue(); // khác bài
        MakeupRules.Check(original, Session(original.ClassId, Day.AddDays(3), lesson), true, now).IsFailure.ShouldBeTrue(); // cùng lớp
        MakeupRules.Check(original, Session(ClassId.New(), Day.AddDays(-1), lesson), true, now).IsFailure.ShouldBeTrue(); // đã qua

        MakeupRules.Overlaps(Day, new(18, 0), new(20, 0), Day, new(19, 30), new(21, 0)).ShouldBeTrue();
        MakeupRules.Overlaps(Day, new(18, 0), new(20, 0), Day, new(20, 0), new(21, 0)).ShouldBeFalse();
        MakeupRules.Overlaps(Day, new(18, 0), new(20, 0), Day.AddDays(1), new(18, 0), new(20, 0)).ShouldBeFalse();
    }

    [Fact]
    public void Doi_bo_buoi_bu_chi_khi_don_con_cho()
    {
        var makeup = ClassSessionId.New();
        var l = Leave("Bị ốm cần nghỉ", Morning).Value;
        l.ChangeMakeup(makeup).IsSuccess.ShouldBeTrue();
        l.MakeupSessionId.ShouldBe(makeup);
        l.ChangeMakeup(null).IsSuccess.ShouldBeTrue();
        l.MakeupSessionId.ShouldBeNull();

        l.Approve(Staff, At, null);
        l.ChangeMakeup(makeup).Error.Code.ShouldBe("academic.request.not_pending");
    }
}
