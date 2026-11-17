using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Classes.Events;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

public sealed class ClassTests
{
    private static readonly BranchId Cg = new(Guid.NewGuid());
    private static readonly CourseId Course = CourseId.New();
    private static readonly CurriculumId Cur = CurriculumId.New();
    private static readonly RoomId Room = RoomId.New();
    private static readonly Guid T1 = Guid.NewGuid(), T2 = Guid.NewGuid(), Ta = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 12, 1);

    private static ClassSetup Setup(string code = "gvv_n3-k12", List<Guid>? teachers = null, bool online = false, RoomId? room = null,
        int min = 8, int max = 18, string? url = null, List<int>? weekdays = null, TimeOnly? end = null, string name = "JLPT N3 K12",
        string? note = null, DateOnly? plannedEnd = null) =>
        new(code, name, Cg, teachers ?? [T1], [Ta], online ? null : room ?? Room, online, url, Start, plannedEnd ?? Start.AddMonths(4),
            weekdays ?? [1, 3, 5], new TimeOnly(18, 0), end ?? new TimeOnly(20, 0), 90, min, max, Money.FromTrusted(6_800_000), null, note);

    private static Class Open(ClassSetup? s = null) => Class.Create(Course, Cur, TeacherType.Vn, s ?? Setup(), 20).Value;

    [Fact]
    public void Mo_lop_chuan_hoa_ma_va_tinh_so_buoi()
    {
        var c = Open();
        c.Code.ShouldBe("GVV_N3-K12");
        c.Status.ShouldBe(ClassStatus.Enrolling);
        c.SessionCount.ShouldBe(45);
        c.AssistantIds.ShouldBe([Ta]);
    }

    [Theory]
    [InlineData("N3-K12", TeacherType.Vn)]
    [InlineData("GVN_N3-K12", TeacherType.Vn)]
    [InlineData("GVV_N3 K12", TeacherType.Vn)]
    [InlineData("GVV_Lớp", TeacherType.Vn)]
    [InlineData("GVV_", TeacherType.Vn)]
    [InlineData("GVV_N3", TeacherType.Mixed)]
    public void Ma_lop_sai_tien_to_hoac_ky_tu(string code, TeacherType type) =>
        Class.NormalizeCode(code, type).Error.Code.ShouldBe("academic.class.invalid_code");

    [Fact]
    public void Tien_to_theo_loai_GV()
    {
        Class.CodePrefix(TeacherType.Vn).ShouldBe("GVV_");
        Class.CodePrefix(TeacherType.Jp).ShouldBe("GVN_");
        Class.CodePrefix(TeacherType.Mixed).ShouldBe("GVVN_");
        Class.NormalizeCode(" gvvn_n2-k01 ", TeacherType.Mixed).Value.ShouldBe("GVVN_N2-K01");
    }

    [Fact]
    public void Quy_tac_mo_lop()
    {
        Result<Class> Make(ClassSetup s, TeacherType t = TeacherType.Vn, int? cap = 20) => Class.Create(Course, Cur, t, s, cap);
        Make(Setup(teachers: [])).Error.Code.ShouldBe("academic.class.teacher_count");
        Make(Setup(teachers: [T1, T2, Guid.NewGuid()])).Error.Code.ShouldBe("academic.class.teacher_count");
        Make(Setup(code: "GVVN_N3", teachers: [T1]), TeacherType.Mixed).Error.Message.ShouldContain("1 GV Việt và 1 GV Nhật");
        Make(Setup(end: new TimeOnly(18, 0))).Error.Code.ShouldBe("academic.class.invalid_time");
        Make(Setup(weekdays: [])).Error.Code.ShouldBe("academic.class.invalid_weekdays");
        Make(Setup(weekdays: [0, 8])).Error.Code.ShouldBe("academic.class.invalid_weekdays");
        Make(Setup(min: 0)).Error.Code.ShouldBe("academic.class.invalid_size");
        Make(Setup(min: 10, max: 8)).Error.Code.ShouldBe("academic.class.invalid_size");
        Make(Setup(max: 25)).Error.Message.ShouldContain("sức chứa phòng (20");
        Make(Setup(plannedEnd: Start.AddDays(-1))).Error.Code.ShouldBe("academic.class.invalid_dates");
        Make(Setup(online: true)).Error.Code.ShouldBe("academic.class.meeting_url_required");
        Make(Setup(name: " ")).Error.Code.ShouldBe("academic.class.name_required");
    }

    [Fact]
    public void Lop_offline_can_phong_online_bo_phong()
    {
        var s = Setup() with { RoomId = null };
        Class.Create(Course, Cur, TeacherType.Vn, s, null).Error.Code.ShouldBe("academic.class.room_required");

        var online = Class.Create(Course, Cur, TeacherType.Vn, Setup(online: true, url: " https://meet.x/a "), null).Value;
        online.RoomId.ShouldBeNull();
        online.MeetingUrl.ShouldBe("https://meet.x/a");
    }

    [Fact]
    public void Sua_khi_dang_hoc_khoa_khoa_hoc_lo_trinh_ma()
    {
        var c = Open();
        c.Start(Start, enrolled: 9).IsSuccess.ShouldBeTrue();

        c.Update(Course, Cur, TeacherType.Vn, Setup(code: "GVV_N3-K13"), 20, 9).Error.Code.ShouldBe("academic.class.locked");
        c.Update(CourseId.New(), Cur, TeacherType.Vn, Setup(), 20, 9).Error.Code.ShouldBe("academic.class.locked");
        c.Update(Course, CurriculumId.New(), TeacherType.Vn, Setup(), 20, 9).Error.Code.ShouldBe("academic.class.locked");

        c.Update(Course, Cur, TeacherType.Vn, Setup(name: "Tên mới", max: 16), 20, 9).IsSuccess.ShouldBeTrue();
        c.Name.ShouldBe("Tên mới");
        c.Update(Course, Cur, TeacherType.Vn, Setup(min: 5, max: 8), 20, 9).Error.Message.ShouldContain("số học viên hiện có (9)");
    }

    [Fact]
    public void Lop_da_dong_chi_sua_ghi_chu()
    {
        var c = Open();
        c.Cancel("Không đủ học viên").IsSuccess.ShouldBeTrue();
        c.Update(Course, Cur, TeacherType.Vn, Setup(note: "  ghi chú mới "), 20, 0).IsSuccess.ShouldBeTrue();
        c.Note.ShouldBe("ghi chú mới");
        c.Update(Course, Cur, TeacherType.Vn, Setup(name: "Khác"), 20, 0).Error.Code.ShouldBe("academic.class.closed");
    }

    [Fact]
    public void Khai_giang_chi_chan_khi_khong_co_hoc_vien()
    {
        var c = Open();
        c.Start(Start, enrolled: 0).Error.Code.ShouldBe("academic.class.not_enough_students");
        c.Start(Start, enrolled: 3).IsSuccess.ShouldBeTrue(); // dưới tối thiểu chỉ cảnh báo (handler)
        c.Status.ShouldBe(ClassStatus.Ongoing);
        c.ActualStartDate.ShouldBe(Start);
        c.DomainEvents.OfType<ClassStarted>().ShouldHaveSingleItem();
        c.Start(Start, 9).Error.Code.ShouldBe("academic.class.not_enrolling");

        Open().Start(Start, enrolled: null).IsSuccess.ShouldBeTrue(); // chưa biết sĩ số → không chặn
    }

    [Fact]
    public void Tam_dung_hoc_lai_ket_thuc()
    {
        var c = Open();
        c.Pause("x").Error.Code.ShouldBe("academic.class.not_ongoing");
        c.Resume().Error.Code.ShouldBe("academic.class.not_paused");
        c.Finish(Start).Error.Code.ShouldBe("academic.class.not_ongoing");

        c.Start(Start, 9);
        c.Pause(" ").Error.Code.ShouldBe("academic.class.pause_reason");
        c.Pause("GV ốm dài ngày").IsSuccess.ShouldBeTrue();
        c.PauseReason.ShouldBe("GV ốm dài ngày");
        c.CanEnroll(1).Error.Code.ShouldBe("academic.class.closed");
        c.Resume().IsSuccess.ShouldBeTrue();
        c.PauseReason.ShouldBeNull();

        c.Finish(Start.AddDays(-1)).Error.Code.ShouldBe("academic.class.invalid_dates");
        c.Finish(Start.AddMonths(4)).IsSuccess.ShouldBeTrue();
        c.Status.ShouldBe(ClassStatus.Finished);
        c.DomainEvents.OfType<ClassFinished>().ShouldHaveSingleItem();
        c.Cancel("x").Error.Code.ShouldBe("academic.class.closed");
    }

    [Fact]
    public void Huy_lop_can_ly_do()
    {
        var c = Open();
        c.Cancel("  ").Error.Code.ShouldBe("academic.class.cancel_reason");
        c.Cancel(" Không đủ HV ").IsSuccess.ShouldBeTrue();
        c.CancelReason.ShouldBe("Không đủ HV");
        c.DomainEvents.OfType<ClassCancelled>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Si_so_toi_da_khi_ghi_danh()
    {
        var c = Open(Setup(min: 1, max: 2));
        c.CanEnroll(1).IsSuccess.ShouldBeTrue();
        c.CanEnroll(2).Error.Code.ShouldBe("academic.class.full");
    }

    [Fact]
    public void Nhan_biet_doi_lich()
    {
        var c = Open();
        c.ScheduleDiffers(Setup()).ShouldBeFalse();
        c.ScheduleDiffers(Setup(weekdays: [2, 4])).ShouldBeTrue();
        c.ScheduleDiffers(Setup(teachers: [T2])).ShouldBeTrue();
        c.ScheduleDiffers(Setup(room: RoomId.New())).ShouldBeTrue();
    }
}
