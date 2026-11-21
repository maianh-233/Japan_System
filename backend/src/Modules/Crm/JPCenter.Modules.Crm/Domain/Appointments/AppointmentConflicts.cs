using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.Appointments;

/// <summary>Kiểm trùng lịch hẹn (CRM-14.E2 → E5) — giống <c>appointments.ts › findConflicts</c>.</summary>
public static class AppointmentConflicts
{
    /// <param name="selfId">Lịch đang đổi (bỏ qua chính nó); null khi đặt mới</param>
    /// <param name="roomId">Phòng đã chọn (null = quầy)</param>
    public static IReadOnlyList<AppointmentConflict> Find(AppointmentId? selfId, LeadId leadId, Guid? roomId, Guid staffId,
        DateTimeOffset start, DateTimeOffset end, ConflictContext ctx)
    {
        var result = new List<AppointmentConflict>();
        if (end <= start) return result;
        var others = ctx.Appointments
            .Where(a => a.Id != selfId && a.Status == AppointmentStatus.Scheduled && a.Overlaps(start, end))
            .OrderBy(a => a.Start).ToList();

        // ---- Phòng ----
        if (roomId is not null)
        {
            var room = ctx.RoomName ?? "Phòng";
            if (ctx.RoomUnavailable is { } why) result.Add(new(ConflictKind.Room, $"{room} đang {why}"));
            foreach (var a in others.Where(a => a.RoomId == roomId))
                result.Add(new(ConflictKind.Room, $"{room} đã có hẹn {KindShort(a.Kind)} {a.Code} ({Hm(a, ctx)}) — {ctx.LeadName(a.LeadId)}"));
            foreach (var s in ctx.RoomSessions.Where(s => s.Start < end && start < s.End))
                result.Add(new(ConflictKind.Room, $"{room} đang có {s.Label}"));
        }

        // ---- Người tiếp ----
        foreach (var a in others.Where(a => a.StaffId == staffId))
            result.Add(new(ConflictKind.Staff, $"{ctx.StaffName} đã có hẹn {a.Code} ({Hm(a, ctx)}) — {ctx.LeadName(a.LeadId)}"));
        foreach (var s in ctx.StaffTeaching.Where(s => s.Start < end && start < s.End))
            result.Add(new(ConflictKind.Staff, $"{ctx.StaffName} đang dạy {s.Label}"));
        if (ctx.StaffOnLongLeave) result.Add(new(ConflictKind.Staff, $"{ctx.StaffName} đang nghỉ phép dài ngày"));

        // ---- Lead đã có hẹn khác cùng giờ ----
        foreach (var a in others.Where(a => a.LeadId == leadId))
            result.Add(new(ConflictKind.Lead, $"Lead này đã có hẹn {a.Code} lúc {Hm(a, ctx)} {ctx.ToLocal(a.Start):dd/MM}"));

        return result;
    }

    private static string Hm(Appointment a, ConflictContext ctx) => $"{ctx.ToLocal(a.Start):HH:mm}–{ctx.ToLocal(a.End):HH:mm}";

    private static string KindShort(AppointmentKind k) => k == AppointmentKind.Test ? "test" : "tư vấn";
}
