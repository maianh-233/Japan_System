using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Application;

/// <summary>DT-09.E5 / HV-14 — chuyên cần HV × lớp: vắng không phép, vắng liên tiếp gần nhất, % đi học (đã học bù tính như có mặt)</summary>
public sealed class AttendanceStatsCalculatorTests
{
    private static readonly ClassId Class = ClassId.New();
    private static readonly Guid Hv = Guid.NewGuid();
    private static readonly DateTimeOffset At = DateTimeOffset.UtcNow;

    private static ClassSession Session(int day, AttendanceStatus? status)
    {
        var s = ClassSession.Create(Class, new BranchId(Guid.NewGuid()), day, new DateOnly(2026, 12, day), new(18, 0), new(20, 0), SessionKind.Lesson,
            null, null, SessionOrigin.Regular, null, "QRTOKEN" + day).Value;
        if (status is { } st) s.RecordAttendance([new AttendanceEntry(Hv, st, null, null)], AttendanceMethod.Teacher, Guid.NewGuid(), At);
        return s;
    }

    [Fact]
    public void Tinh_vang_lien_tiep_gan_nhat_va_ti_le_di_hoc()
    {
        var sessions = new[]
        {
            Session(1, AttendanceStatus.Absent), Session(2, AttendanceStatus.Present), Session(3, AttendanceStatus.Excused),
            Session(4, AttendanceStatus.Absent), Session(5, AttendanceStatus.Absent), Session(6, null),
        };
        var s = AttendanceStatsCalculator.Compute(Class.Value, Hv, sessions);
        (s.Taken, s.Absent, s.Streak, s.AttendanceRate).ShouldBe((5, 3, 2, 20));
    }

    [Fact]
    public void Buoi_huy_khong_tinh_buoi_vang_da_hoc_bu_tinh_di_hoc()
    {
        var cancelled = Session(1, null);
        cancelled.Cancel("Nghỉ lễ", Guid.NewGuid(), At, new DateTime(2026, 11, 1)).IsSuccess.ShouldBeTrue();
        var madeUp = Session(2, AttendanceStatus.Absent);
        madeUp.MarkMadeUp(Hv, ClassSessionId.New(), AttendanceStatus.Present);

        var s = AttendanceStatsCalculator.Compute(Class.Value, Hv, [cancelled, madeUp, Session(3, AttendanceStatus.Late)]);
        (s.Taken, s.Absent, s.Streak, s.AttendanceRate).ShouldBe((2, 0, 0, 100));
        AttendanceStatsCalculator.Compute(Class.Value, Guid.NewGuid(), [madeUp]).AttendanceRate.ShouldBeNull();
    }
}
