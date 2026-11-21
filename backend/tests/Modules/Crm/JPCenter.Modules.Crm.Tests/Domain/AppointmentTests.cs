using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Tests.Domain;

public sealed class AppointmentTests
{
    private static readonly BranchId Branch = new(Guid.NewGuid());
    private static readonly Guid Room = Guid.NewGuid(), Staff = Guid.NewGuid(), Staff2 = Guid.NewGuid();
    private static readonly LeadId LeadA = LeadId.New(), LeadB = LeadId.New();
    /// <summary>Bây giờ: 09:00 UTC (16:00 giờ VN)</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

    private static Appointment Book(LeadId lead, DateTimeOffset start, int minutes = 60, Guid? room = null, Guid? staff = null,
        string code = "LH0001") =>
        Appointment.Create(code, lead, AppointmentKind.Test, Branch, room, staff ?? Staff, start, start.AddMinutes(minutes), null, Now).Value;

    private static ConflictContext Ctx(IReadOnlyList<Appointment> appts, string? roomUnavailable = null, BusySlot[]? roomSessions = null,
        BusySlot[]? teaching = null, bool leave = false) =>
        new(appts, "P.201", roomUnavailable, roomSessions ?? [], "Hồ Quỳnh Như", leave, teaching ?? [],
            id => id == LeadA ? "Nguyễn Văn A" : "Trần Thị B", t => t.ToOffset(TimeSpan.FromHours(7)).DateTime);

    [Fact]
    public void CRM_14_E1_gio_khong_hop_le()
    {
        Appointment.Create("x", LeadA, AppointmentKind.Consult, Branch, null, Staff, Now.AddHours(2), Now.AddHours(2), null, Now).Error
            .ShouldBe(AppointmentErrors.InvalidTime);
        Appointment.Create("x", LeadA, AppointmentKind.Consult, Branch, null, Staff, Now.AddHours(-1), Now, null, Now).Error
            .ShouldBe(AppointmentErrors.InvalidTime);
        Appointment.CodeFor(3).ShouldBe("LH0003");
        (Appointment.DefaultDuration(AppointmentKind.Consult), Appointment.DefaultDuration(AppointmentKind.Test)).ShouldBe((30, 60));
    }

    [Fact]
    public void CRM_14_trung_phong_nguoi_tiep_lead_va_bo_qua_lich_da_huy_va_chinh_no()
    {
        var other = Book(LeadB, Now.AddHours(2), room: Room, staff: Staff, code: "LH0002");
        var cancelled = Book(LeadA, Now.AddHours(2), room: Room, code: "LH0003");
        cancelled.Cancel("Khách bận");
        var mine = Book(LeadA, Now.AddHours(2).AddMinutes(30), room: Room, staff: Staff2, code: "LH0004");
        var conflicts = AppointmentConflicts.Find(null, LeadB, Room, Staff, Now.AddHours(2).AddMinutes(30), Now.AddHours(3), Ctx([other, cancelled, mine]));

        conflicts.Select(c => c.Kind).ShouldBe([ConflictKind.Room, ConflictKind.Room, ConflictKind.Staff, ConflictKind.Lead]);
        conflicts[0].Message.ShouldBe("P.201 đã có hẹn test LH0002 (18:00–19:00) — Trần Thị B");
        conflicts[2].Message.ShouldBe("Hồ Quỳnh Như đã có hẹn LH0002 (18:00–19:00) — Trần Thị B");
        conflicts[3].Message.ShouldBe("Lead này đã có hẹn LH0002 lúc 18:00–19:00 21/11");

        AppointmentConflicts.Find(other.Id, LeadB, Room, Staff, other.Start, other.End, Ctx([other])).ShouldBeEmpty();
    }

    [Fact]
    public void CRM_14_E2_E3_E4_phong_bao_tri_buoi_hoc_gio_day_nghi_dai_ngay()
    {
        var s = Now.AddHours(2);
        var session = new BusySlot(s.AddMinutes(-30), s.AddMinutes(90), "lớp N3K12 học 17:30–19:30");
        var conflicts = AppointmentConflicts.Find(null, LeadA, Room, Staff, s, s.AddHours(1),
            Ctx([], roomUnavailable: "bảo trì", roomSessions: [session], teaching: [session with { Label = "lớp N3K12 (17:30–19:30)" }], leave: true));

        conflicts.Select(c => c.Message).ShouldBe([
            "P.201 đang bảo trì",
            "P.201 đang có lớp N3K12 học 17:30–19:30",
            "Hồ Quỳnh Như đang dạy lớp N3K12 (17:30–19:30)",
            "Hồ Quỳnh Như đang nghỉ phép dài ngày",
        ]);

        // Hẹn ở quầy (không phòng) + buổi học không giao giờ → không trùng
        AppointmentConflicts.Find(null, LeadA, null, Staff, s.AddHours(2), s.AddHours(3), Ctx([], roomUnavailable: "bảo trì",
            teaching: [session])).ShouldBeEmpty();
    }

    [Fact]
    public void CRM_15_da_den_can_ket_qua_khong_den_sau_gio_huy_can_ly_do()
    {
        var a = Book(LeadA, Now.AddHours(1));
        a.MarkDone(" ").Error.ShouldBe(AppointmentErrors.OutcomeRequired);
        a.MarkNoShow(Now).Error.ShouldBe(AppointmentErrors.NotStarted);
        a.Cancel("").Error.ShouldBe(AppointmentErrors.CancelReasonRequired);

        a.MarkDone("Khách ưng lớp N4 tối").IsSuccess.ShouldBeTrue();
        (a.Status, a.Outcome).ShouldBe((AppointmentStatus.Done, "Khách ưng lớp N4 tối"));
        a.Cancel("x").Error.ShouldBe(AppointmentErrors.NotScheduled);
        a.MarkNoShow(Now.AddHours(2)).Error.ShouldBe(AppointmentErrors.NotScheduled);

        var b = Book(LeadA, Now.AddHours(1));
        b.MarkNoShow(Now.AddHours(1)).IsSuccess.ShouldBeTrue();
        b.Status.ShouldBe(AppointmentStatus.NoShow);
    }

    [Fact]
    public void Nhac_truoc_60_phut_mot_lan_doi_lich_nhac_lai()
    {
        var a = Book(LeadA, Now.AddMinutes(90));
        a.NeedsReminder(Now).ShouldBeFalse();
        a.NeedsReminder(Now.AddMinutes(30)).ShouldBeTrue();
        a.MarkReminded(Now.AddMinutes(30));
        a.NeedsReminder(Now.AddMinutes(40)).ShouldBeFalse();

        a.Reschedule(Now.AddDays(1), Now.AddDays(1).AddHours(1), null, Staff2, "Đổi sáng mai", Now).IsSuccess.ShouldBeTrue();
        (a.RemindedAt, a.RoomId, a.StaffId, a.Note).ShouldBe(((DateTimeOffset?)null, (Guid?)null, Staff2, "Đổi sáng mai"));
        a.Reschedule(Now.AddHours(-2), Now, null, Staff, null, Now).Error.ShouldBe(AppointmentErrors.InvalidTime);
        a.Cancel("Khách hủy");
        a.Reschedule(Now.AddDays(2), Now.AddDays(2).AddHours(1), null, Staff, null, Now).Error.ShouldBe(AppointmentErrors.NotScheduled);
        a.NeedsReminder(Now.AddDays(2).AddMinutes(-10)).ShouldBeFalse();
    }
}
