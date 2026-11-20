using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

/// <summary>GĐ 15 — điểm danh QR (DT-09), GV tự chấm công (DT-12), báo cáo buổi (DT-14), nhận dạy thay (DT-16)</summary>
public sealed class SessionPortalTests
{
    private static readonly DateOnly Day = new(2026, 12, 7);
    private static DateTime T(int h, int m = 0) => Day.ToDateTime(new TimeOnly(h, m));
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;
    private static readonly Guid Hv = Guid.NewGuid(), Gv = Guid.NewGuid(), Staff = Guid.NewGuid();
    private const string Token = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";

    // Buổi 18:00 – 20:00
    private static ClassSession New() =>
        ClassSession.Create(ClassId.New(), new BranchId(Guid.NewGuid()), 1, Day, new TimeOnly(18, 0), new TimeOnly(20, 0), SessionKind.Lesson,
            LessonId.New(), null, SessionOrigin.Regular, null, Token).Value;

    [Fact]
    public void Ma_6_ky_tu_la_dau_ma_QR_chu_hoa()
    {
        New().ShortCode.ShouldBe("A1B2C3");
    }

    [Fact]
    public void Quet_QR_dung_gio_co_mat_qua_10_phut_la_muon()
    {
        var s = New();
        s.ScanQr(Hv, "a1b2c3", true, T(17, 35), At).Value.ShouldBe(AttendanceStatus.Present);
        var r = s.Attendance.Single();
        (r.Method, r.CheckInAt).ShouldBe((AttendanceMethod.Qr, new TimeOnly(17, 35)));

        var other = Guid.NewGuid();
        s.ScanQr(other, Token, true, T(18, 11), At).Value.ShouldBe(AttendanceStatus.Late);
        s.ScanQr(Guid.NewGuid(), Token, true, T(18, 10), At).Value.ShouldBe(AttendanceStatus.Present); // đúng 10 phút vẫn có mặt
    }

    [Fact]
    public void Quet_lan_2_giu_ket_qua_lan_dau()
    {
        var s = New();
        s.ScanQr(Hv, Token, true, T(18, 20), At).Value.ShouldBe(AttendanceStatus.Late);
        s.ScanQr(Hv, Token, true, T(18, 25), At).Value.ShouldBe(AttendanceStatus.Late); // E8
        s.Attendance.Single().CheckInAt.ShouldBe(new TimeOnly(18, 20));
    }

    [Fact]
    public void Quet_QR_loi_ma_ngoai_danh_sach_ngoai_gio_buoi_huy()
    {
        var s = New();
        s.ScanQr(Hv, "ZZZZZZ", true, T(18), At).Error.Code.ShouldBe("academic.attendance.invalid_code");
        s.ScanQr(Hv, Token, false, T(18), At).Error.Code.ShouldBe("academic.attendance.not_in_roster");
        s.ScanQr(Hv, Token, true, T(17, 29), At).Error.Code.ShouldBe("academic.attendance.qr_closed");
        s.ScanQr(Hv, Token, true, T(20, 1), At).Error.Code.ShouldBe("academic.attendance.qr_closed");

        s.Cancel("Nghỉ lễ", Staff, At, T(17)).IsSuccess.ShouldBeTrue();
        s.ScanQr(Hv, Token, true, T(18), At).Error.Code.ShouldBe("academic.session.cancelled");
    }

    [Fact]
    public void Khach_hoc_bu_quet_duoc_du_khong_ghi_danh_lop()
    {
        var s = New();
        s.AddMakeupGuest(Hv, ClassSessionId.New(), Guid.NewGuid());
        s.ScanQr(Hv, Token, false, T(18), At).Value.ShouldBe(AttendanceStatus.Present);
        s.Attendance.Single().IsGuest.ShouldBeTrue();
    }

    [Fact]
    public void Don_nghi_duyet_sau_khi_diem_danh_vang_thanh_co_phep_co_mat_giu_nguyen()
    {
        var s = New();
        var hv2 = Guid.NewGuid();
        s.RecordAttendance([new(Hv, AttendanceStatus.Absent, null, null), new(hv2, AttendanceStatus.Present, new TimeOnly(18, 0), null)],
            AttendanceMethod.Teacher, Gv, At).IsSuccess.ShouldBeTrue();
        s.MarkExcusedByLeave(Hv, Guid.NewGuid());
        s.MarkExcusedByLeave(hv2, Guid.NewGuid());
        s.Attendance.Single(a => a.StudentId == Hv).Status.ShouldBe(AttendanceStatus.Excused);
        s.Attendance.Single(a => a.StudentId == hv2).Status.ShouldBe(AttendanceStatus.Present); // E7
    }

    [Fact]
    public void GV_tu_check_in_tu_30_phut_truoc_sau_gio_hoc_la_muon()
    {
        var s = New();
        s.SelfCheckIn(Gv, StaffRole.Teacher, T(17, 29), At).Error.Code.ShouldBe("academic.teacher_attendance.too_early");
        s.SelfCheckOut(Gv, T(19), At).Error.Code.ShouldBe("academic.teacher_attendance.not_checked_in"); // E2

        s.SelfCheckIn(Gv, StaffRole.Teacher, T(17, 50), At).IsSuccess.ShouldBeTrue();
        s.SelfCheckIn(Gv, StaffRole.Teacher, T(18, 30), At).IsSuccess.ShouldBeTrue(); // bấm lại giữ giờ đầu
        var t = s.TeacherAttendance.Single();
        (t.Status, t.CheckIn, t.Source).ShouldBe((TeacherAttendanceStatus.Present, new TimeOnly(17, 50), CheckSource.Self));

        var ta = Guid.NewGuid();
        s.SelfCheckIn(ta, StaffRole.Assistant, T(18, 5), At).IsSuccess.ShouldBeTrue();
        s.TeacherAttendance.Single(x => x.TeacherId == ta).Status.ShouldBe(TeacherAttendanceStatus.Late);

        s.SelfCheckOut(Gv, T(20, 2), At).IsSuccess.ShouldBeTrue();
        s.TeacherAttendance.Single(x => x.TeacherId == Gv).CheckOut.ShouldBe(new TimeOnly(20, 2));
    }

    [Fact]
    public void Ket_thuc_buoi_tu_check_out_va_bat_buoc_noi_dung()
    {
        var s = New();
        s.SelfCheckIn(Gv, StaffRole.Teacher, T(17, 55), At).IsSuccess.ShouldBeTrue();
        s.SubmitReport(" ", null, null, LessonCoverage.Full, [], Gv, At, T(20)).Error.Code.ShouldBe("academic.session.report_required");
        s.SubmitReport("Bài 5 — từ vựng", " ", null, LessonCoverage.Full, [], Gv, At, T(20)).IsSuccess.ShouldBeTrue();
        s.Report!.Homework.ShouldBeNull();
        s.TeacherAttendance.Single().CheckOut.ShouldBe(new TimeOnly(20, 0));
    }

    [Fact]
    public void GV_khong_tu_cham_cong_sau_khi_buoi_da_xac_nhan()
    {
        var s = New();
        s.RecordAttendance([new(Hv, AttendanceStatus.Present, new TimeOnly(18, 0), null)], AttendanceMethod.Teacher, Gv, At);
        s.Confirm(Staff, At, null, T(21)).IsSuccess.ShouldBeTrue();
        s.SelfCheckIn(Gv, StaffRole.Teacher, T(21), At).Error.Code.ShouldBe("academic.session.confirmed_locked");
        s.ScanQr(Guid.NewGuid(), Token, true, T(19), At).Error.Code.ShouldBe("academic.attendance.qr_closed");
    }

    [Fact]
    public void Nhan_day_thay_chi_truoc_gio_hoc()
    {
        var s = New();
        var sub = Guid.NewGuid();
        s.AssignSubstitute(sub, "Dạy thay GV ốm", T(18)).Error.Code.ShouldBe("academic.session.already_started");
        s.AssignSubstitute(sub, "Dạy thay GV ốm", T(12)).IsSuccess.ShouldBeTrue();
        s.TeacherIds.ShouldBe([sub]);
        s.HasOverrides.ShouldBeTrue();
    }
}
