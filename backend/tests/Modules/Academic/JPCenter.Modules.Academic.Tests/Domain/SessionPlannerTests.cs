using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Tests.Domain;

public sealed class SessionPlannerTests
{
    private static readonly LessonId L1 = LessonId.New(), L2 = LessonId.New(), L3 = LessonId.New();
    private static readonly ExamMilestoneId Mid = ExamMilestoneId.New(), Final = ExamMilestoneId.New();
    private static readonly Dictionary<DateOnly, string> NoHolidays = [];

    [Fact]
    public void Chia_bai_deu_va_chen_buoi_thi_sau_bai_cua_moc()
    {
        var slots = SessionPlanner.BuildSlots(8, [L1, L2, L3], [(Mid, L2), (Final, L3)]);
        // 8 buổi: 2 thi + 6 học → mỗi bài 2 buổi
        slots.Select(s => s.LessonId?.ToString() ?? s.ExamId!.Value.ToString()).ShouldBe(
        [
            L1.ToString(), L1.ToString(), L2.ToString(), L2.ToString(), Mid.ToString(), L3.ToString(), L3.ToString(), Final.ToString(),
        ]);
    }

    [Fact]
    public void Khong_co_lo_trinh_la_buoi_hoc_chua_gan_bai()
    {
        var slots = SessionPlanner.BuildSlots(3, [], []);
        slots.Count.ShouldBe(3);
        slots.ShouldAllBe(s => s.Kind == SessionKind.Lesson && s.LessonId == null);
    }

    [Fact]
    public void Sinh_theo_thu_trong_tuan_bo_ngay_le()
    {
        // 2026-12-07 là Thứ 2
        var holidays = new Dictionary<DateOnly, string> { [new DateOnly(2026, 12, 9)] = "Lễ thử" };
        var plan = SessionPlanner.Plan(new DateOnly(2026, 12, 7), [1, 3, 5], 4, 1, 0, holidays, skipHolidays: true, [], []);
        plan.Sessions.Select(s => s.Date).ShouldBe([new(2026, 12, 7), new(2026, 12, 11), new(2026, 12, 14), new(2026, 12, 16)]);
        plan.Sessions.Select(s => s.No).ShouldBe([1, 2, 3, 4]);
        plan.Skipped.ShouldHaveSingleItem().Name.ShouldBe("Lễ thử");
        plan.EndDate.ShouldBe(new DateOnly(2026, 12, 16));

        SessionPlanner.Plan(new DateOnly(2026, 12, 7), [3], 1, 1, 0, holidays, skipHolidays: false, [], [])
            .Sessions.Single().Date.ShouldBe(new DateOnly(2026, 12, 9));
    }

    [Fact]
    public void Sinh_phan_con_lai_danh_so_tiep_va_bo_noi_dung_da_day()
    {
        var plan = SessionPlanner.Plan(new DateOnly(2026, 12, 7), [1], 2, startNo: 3, skipSlots: 2, NoHolidays, true, [L1, L2], []);
        plan.Sessions.Select(s => s.No).ShouldBe([3, 4]);
        plan.Sessions.ShouldAllBe(s => s.Slot.LessonId == L2);
    }

    [Fact]
    public void Dau_vao_sai_tra_ve_rong()
    {
        SessionPlanner.Plan(new DateOnly(2026, 12, 7), [], 3, 1, 0, NoHolidays, true, [], []).Sessions.ShouldBeEmpty();
        SessionPlanner.Plan(new DateOnly(2026, 12, 7), [1], 0, 1, 0, NoHolidays, true, [], []).Sessions.ShouldBeEmpty();
        SessionPlanner.WeekdayOf(new DateOnly(2026, 12, 13)).ShouldBe(7);
    }
}
