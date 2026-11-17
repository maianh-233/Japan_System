using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.ClassSessions.Events;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

public sealed class ClassSessionTests
{
    private static readonly DateOnly Day = new(2026, 12, 7);
    private static readonly DateTime Before = Day.ToDateTime(new TimeOnly(17, 0));
    private static readonly DateTime During = Day.ToDateTime(new TimeOnly(19, 0));
    private static readonly DateTime After = Day.ToDateTime(new TimeOnly(21, 0));
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;
    private static readonly Guid Staff = Guid.NewGuid(), Hv1 = Guid.NewGuid(), Hv2 = Guid.NewGuid(), Gv = Guid.NewGuid();

    private static ClassSession New(SessionOrigin origin = SessionOrigin.Regular, ClassSessionId? makeupFor = null) =>
        ClassSession.Create(ClassId.New(), new BranchId(Guid.NewGuid()), 3, Day, new TimeOnly(18, 0), new TimeOnly(20, 0), SessionKind.Lesson,
            LessonId.New(), null, origin, makeupFor, "QR").Value;

    [Fact]
    public void Tao_buoi_kiem_gio_va_buoi_bu()
    {
        ClassSession.Create(ClassId.New(), new BranchId(Guid.NewGuid()), 1, Day, new TimeOnly(18, 0), new TimeOnly(18, 0), SessionKind.Lesson,
            null, null, SessionOrigin.Regular, null, "Q").Error.Code.ShouldBe("academic.session.invalid_time");
        ClassSession.Create(ClassId.New(), new BranchId(Guid.NewGuid()), 1, Day, new TimeOnly(18, 0), new TimeOnly(20, 0), SessionKind.Lesson,
            null, null, SessionOrigin.Makeup, null, "Q").Error.Code.ShouldBe("academic.session.makeup_for_required");
        New(SessionOrigin.Extra).No.ShouldBe(0); // học thêm không đánh số chuỗi
    }

    [Fact]
    public void Sua_buoi_luu_lich_goc_va_can_ly_do()
    {
        var s = New();
        s.Edit(Day, new TimeOnly(18, 0), new TimeOnly(20, 0), [], null, null, " ").Error.Code.ShouldBe("academic.session.reason_required");
        s.Edit(Day, new TimeOnly(20, 0), new TimeOnly(19, 0), [], null, null, "x").Error.Code.ShouldBe("academic.session.invalid_time");

        s.Edit(Day.AddDays(1), new TimeOnly(19, 0), new TimeOnly(21, 0), [Gv], null, RoomId.New(), "GV bận").IsSuccess.ShouldBeTrue();
        s.Edit(Day.AddDays(2), new TimeOnly(19, 0), new TimeOnly(21, 0), [Gv], null, null, "Dời lần 2").IsSuccess.ShouldBeTrue();
        s.RescheduledFromDate.ShouldBe(Day); // giữ lịch GỐC
        s.Date.ShouldBe(Day.AddDays(2));
        s.TeacherIds.ShouldBe([Gv]);
        s.HasOverrides.ShouldBeTrue();
        s.IsReplaceable(Before).ShouldBeTrue(); // DT-08.E3: buổi có thay đổi riêng vẫn bị thay (hộp xác nhận liệt kê)
    }

    [Fact]
    public void Huy_buoi_chi_truoc_gio_hoc()
    {
        var s = New();
        s.Cancel("x", Staff, At, During).Error.Code.ShouldBe("academic.session.already_started");
        s.Cancel(" ", Staff, At, Before).Error.Code.ShouldBe("academic.session.reason_required");
        s.Cancel("GV ốm", Staff, At, Before).IsSuccess.ShouldBeTrue();
        s.Status.ShouldBe(SessionStatus.Cancelled);
        s.DomainEvents.OfType<SessionCancelled>().ShouldHaveSingleItem();
        s.Cancel("x", Staff, At, Before).Error.Code.ShouldBe("academic.session.cancelled");
        s.RecordAttendance([new(Hv1, AttendanceStatus.Present, null, null)], AttendanceMethod.Staff, Staff, At)
            .Error.Code.ShouldBe("academic.session.cancelled");
    }

    [Fact]
    public void Diem_danh_ve_som_va_vang_xoa_gio_vao()
    {
        var s = New();
        s.RecordAttendance(
        [
            new(Hv1, AttendanceStatus.Late, new TimeOnly(18, 15), " muộn ", new TimeOnly(19, 30), "Đau bụng"),
            new(Hv2, AttendanceStatus.Absent, new TimeOnly(18, 0), null, new TimeOnly(19, 0), "x"),
        ], AttendanceMethod.Teacher, Gv, At).IsSuccess.ShouldBeTrue();

        var r1 = s.Attendance.Single(r => r.StudentId == Hv1);
        (r1.Status, r1.CheckInAt, r1.Note, r1.LeftAt, r1.LeaveReason).ShouldBe((AttendanceStatus.Late, new TimeOnly(18, 15), "muộn", new TimeOnly(19, 30), "Đau bụng"));
        var r2 = s.Attendance.Single(r => r.StudentId == Hv2);
        (r2.CheckInAt, r2.LeftAt).ShouldBe((null, null));
        s.AttendanceBy.ShouldBe(Gv);
        s.IsReplaceable(Before).ShouldBeFalse(); // đã điểm danh
    }

    [Fact]
    public void Don_nghi_duyet_sau_khong_doi_hoc_vien_da_den()
    {
        var s = New();
        s.RecordAttendance([new(Hv1, AttendanceStatus.Present, null, null), new(Hv2, AttendanceStatus.Absent, null, null)],
            AttendanceMethod.Staff, Staff, At);
        var leave = Guid.NewGuid();
        s.MarkExcusedByLeave(Hv1, leave);
        s.MarkExcusedByLeave(Hv2, leave);
        s.Attendance.Single(r => r.StudentId == Hv1).Status.ShouldBe(AttendanceStatus.Present);
        s.Attendance.Single(r => r.StudentId == Hv2).Status.ShouldBe(AttendanceStatus.Excused);
    }

    [Fact]
    public void Cham_cong_GV()
    {
        var s = New();
        s.RecordStaffAttendance(Gv, StaffRole.Teacher, CheckSource.Staff, TeacherAttendanceStatus.Present, null, new TimeOnly(20, 0), null, null, At)
            .Error.Code.ShouldBe("academic.teacher_attendance.not_checked_in");
        s.RecordStaffAttendance(Gv, StaffRole.Teacher, CheckSource.Staff, TeacherAttendanceStatus.Present, new TimeOnly(20, 0), new TimeOnly(19, 0), null, null, At)
            .Error.Code.ShouldBe("academic.session.invalid_time");

        var sub = Guid.NewGuid();
        s.RecordStaffAttendance(Gv, StaffRole.Teacher, CheckSource.Staff, TeacherAttendanceStatus.Absent, new TimeOnly(18, 0), null, sub, "ốm", At)
            .IsSuccess.ShouldBeTrue();
        var t = s.TeacherAttendance.Single();
        (t.CheckIn, t.SubstituteId, t.Note).ShouldBe((null, sub, "ốm"));
    }

    [Fact]
    public void Su_co_chi_khi_buoi_da_bat_dau()
    {
        var s = New();
        var inc = new SessionIncident(new TimeOnly(19, 0), IncidentReason.Sick, null, IncidentAction.Assistant, null, true, Staff, At);
        s.ReportIncident(inc, Before).Error.Code.ShouldBe("academic.session.not_started");
        s.ResolveIncident(IncidentOutcome.Resumed, At, null, Staff).Error.Code.ShouldBe("academic.session.no_incident");
        s.ReportIncident(inc, During).IsSuccess.ShouldBeTrue();
        s.ResolveIncident(IncidentOutcome.Ended, At, null, Staff).IsSuccess.ShouldBeTrue();
        s.Incident!.Outcome.ShouldBe(IncidentOutcome.Ended);
    }

    [Fact]
    public void Xac_nhan_va_bo_xac_nhan()
    {
        var s = New();
        s.Confirm(Staff, At, null, During).Error.Code.ShouldBe("academic.session.not_finished");
        s.Confirm(Staff, At, null, After).Error.Code.ShouldBe("academic.session.not_attended");

        s.RecordAttendance([new(Hv1, AttendanceStatus.Present, null, null)], AttendanceMethod.Teacher, Gv, At);
        s.RecordStaffAttendance(Gv, StaffRole.Teacher, CheckSource.Self, TeacherAttendanceStatus.Present, new TimeOnly(18, 0), new TimeOnly(20, 0), null, null, At);
        s.Confirm(Staff, At, " ok ", After).IsSuccess.ShouldBeTrue();
        (s.Status, s.ConfirmNote).ShouldBe((SessionStatus.Completed, "ok"));
        s.DomainEvents.OfType<SessionConfirmed>().Single().Teachers.ShouldHaveSingleItem().TeacherId.ShouldBe(Gv);
        s.Confirm(Staff, At, null, After).Error.Code.ShouldBe("academic.session.already_confirmed");

        // Đã xác nhận: GV không sửa, giáo vụ sửa được
        s.RecordAttendance([new(Hv1, AttendanceStatus.Late, null, null)], AttendanceMethod.Teacher, Gv, At).Error.Code.ShouldBe("academic.session.confirmed_locked");
        s.RecordAttendance([new(Hv1, AttendanceStatus.Late, null, null)], AttendanceMethod.Staff, Staff, At).IsSuccess.ShouldBeTrue();
        s.Edit(Day, new TimeOnly(18, 0), new TimeOnly(20, 0), [], null, null, "x").Error.Code.ShouldBe("academic.session.not_scheduled");

        s.Unconfirm().IsSuccess.ShouldBeTrue();
        (s.Status, s.ConfirmedAt).ShouldBe((SessionStatus.Scheduled, null));
        s.DomainEvents.OfType<SessionUnconfirmed>().ShouldHaveSingleItem();
        s.Unconfirm().Error.Code.ShouldBe("academic.session.not_confirmed");
    }

    [Fact]
    public void Bao_cao_buoi_khoa_sau_24h_tu_luc_xac_nhan()
    {
        var s = New();
        s.SubmitReport("Bài 3", null, null, LessonCoverage.Full, [], Gv, At).IsSuccess.ShouldBeTrue();
        s.Status.ShouldBe(SessionStatus.Scheduled); // chờ xác nhận, không tự xác nhận
        var ended = s.Report!.EndedAt;

        s.RecordAttendance([new(Hv1, AttendanceStatus.Present, null, null)], AttendanceMethod.Teacher, Gv, At);
        s.Confirm(Staff, At, null, After);
        s.SubmitReport("Sửa", null, null, LessonCoverage.Partial, [], Gv, At.AddHours(2)).IsSuccess.ShouldBeTrue();
        s.Report!.EndedAt.ShouldBe(ended);
        s.SubmitReport("Sửa", null, null, LessonCoverage.Partial, [], Gv, At.AddHours(25)).Error.Code.ShouldBe("academic.session.report_locked");
    }

    [Fact]
    public void Buoi_thay_duoc_khi_sinh_lai_lich()
    {
        New().IsReplaceable(Before).ShouldBeTrue();
        New().IsReplaceable(During).ShouldBeFalse(); // đã bắt đầu
        New(SessionOrigin.Extra).IsReplaceable(Before).ShouldBeFalse();
        New(SessionOrigin.Makeup, ClassSessionId.New()).IsReplaceable(Before).ShouldBeFalse();
    }
}
