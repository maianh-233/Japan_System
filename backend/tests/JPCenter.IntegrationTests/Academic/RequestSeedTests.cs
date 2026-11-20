using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Promotions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Academic;

/// <summary>Giai đoạn 15 phase 2 — dữ liệu mẫu đơn từ, lời mời dạy thay, lên lớp, nhận xét, chăm sóc HV nạp đúng từ mock frontend.</summary>
[Collection(SeededCollection.Name)]
public sealed class RequestSeedTests(SeededApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Don_huy_buoi_loi_moi_day_thay()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();

        var cancels = await db.SessionCancelRequests.AsNoTracking().ToListAsync(Ct);
        cancels.Select(c => c.Status).Order().ShouldBe([RequestStatus.Pending, RequestStatus.Approved, RequestStatus.Rejected]);
        var cr1 = cancels.Single(c => c.Id == new SessionCancelRequestId(SeedData.IdOf("CR-1")));
        (cr1.TeacherId, cr1.ProposedMakeupAt is not null).ShouldBe((SeedData.IdOf("t-ha"), true));
        cancels.Single(c => c.Status == RequestStatus.Approved).DecidedBy.ShouldBe(SeedData.IdOf("e-mai"));

        var invites = await db.SubstituteInvites.AsNoTracking().ToListAsync(Ct);
        invites.Count.ShouldBe(3);
        invites.ShouldAllBe(i => i.ToTeacherId == SeedData.IdOf("t-ha"));
        invites.Single(i => i.Status == InviteStatus.Declined).DeclineReason.ShouldBe("Trùng lịch dạy lớp N2-K05.");
    }

    [Fact]
    public async Task Don_xin_nghi_ap_len_diem_danh_buoi_goc_va_buoi_bu()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();

        var leaves = await db.LeaveRequests.AsNoTracking().ToListAsync(Ct);
        leaves.Count.ShouldBe(5);
        var lr1 = leaves.Single(l => l.Id == new LeaveRequestId(SeedData.IdOf("LR-1")));
        (lr1.Status, lr1.By, lr1.ByAccountId).ShouldBe((RequestStatus.Approved, RequestedBy.Parent, SeedData.IdOf("ACC-G-1")));

        // Buổi gốc: vắng có phép theo đơn
        var original = await db.ClassSessions.AsNoTracking().SingleAsync(s => s.Id == lr1.SessionId, Ct);
        var record = original.Attendance.Single(a => a.StudentId == SeedData.IdOf("ST-1"));
        (record.Status, record.LeaveRequestId).ShouldBe((AttendanceStatus.Excused, (Guid?)lr1.Id.Value));

        // Học bù ở lớp khác: buổi bù có HV khách, buổi gốc ghi kết quả học bù
        var lr4 = leaves.Single(l => l.Id == new LeaveRequestId(SeedData.IdOf("LR-4")));
        (lr4.MakeupSessionId is not null, lr4.MakeupResult).ShouldBe((true, (AttendanceStatus?)AttendanceStatus.Present));
        var makeup = await db.ClassSessions.AsNoTracking().SingleAsync(s => s.Id == lr4.MakeupSessionId, Ct);
        var guest = makeup.Attendance.Single(a => a.StudentId == lr4.StudentId);
        (guest.IsGuest, guest.Status, guest.MakeupForSessionId).ShouldBe((true, AttendanceStatus.Present, (ClassSessionId?)lr4.SessionId));
        var source = await db.ClassSessions.AsNoTracking().SingleAsync(s => s.Id == lr4.SessionId, Ct);
        source.Attendance.Single(a => a.StudentId == lr4.StudentId).MadeUpStatus.ShouldBe(AttendanceStatus.Present);

        // Đơn sắp tới đã duyệt: HV đã có tên ở buổi bù, chưa có kết quả
        var lr5 = leaves.Single(l => l.Id == new LeaveRequestId(SeedData.IdOf("LR-5")));
        var upcoming = await db.ClassSessions.AsNoTracking().SingleAsync(s => s.Id == lr5.MakeupSessionId, Ct);
        upcoming.Attendance.Single(a => a.StudentId == lr5.StudentId).IsGuest.ShouldBeTrue();
        lr5.MakeupResult.ShouldBeNull();
    }

    [Fact]
    public async Task Len_lop_va_nhan_xet_cuoi_khoa()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();

        var promos = await db.PromotionCandidates.AsNoTracking().ToListAsync(Ct);
        promos.Count.ShouldBe(6);
        promos.Count(p => p.SeatHeld).ShouldBe(6);
        var handed = promos.Where(p => p.HandedOverAt != null).ToList();
        handed.Count.ShouldBe(2);
        handed.ShouldAllBe(p => p.SaleId == SeedData.IdOf("e-tung"));
        var consulting = promos.Single(p => p.Status == PromotionStatus.Consulting);
        (consulting.InterestBy, consulting.Notes.Count).ShouldBe(((InterestBy?)InterestBy.Student, 1));
        consulting.Notes[0].By.ShouldBe(SeedData.IdOf("e-tung"));
        promos.Count(p => p.Status == PromotionStatus.Unreachable).ShouldBe(1);

        var evals = await db.StudentEvaluations.AsNoTracking().ToListAsync(Ct);
        evals.Count.ShouldBe(4);
        evals.ShouldContain(e => e.Recommendation == EvalRecommendation.Repeat && e.Grammar == 2);
    }

    [Fact]
    public async Task Cham_soc_hoc_vien()
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<StudentsDbContext>();

        var cases = await db.CareCases.AsNoTracking().ToListAsync(Ct);
        cases.Count.ShouldBe(2);
        var c = cases.Single(x => x.ClassId == SeedData.IdOf("C-N3K12"));
        (c.AssigneeId, c.Status, c.NextAt is not null).ShouldBe(((Guid?)SeedData.IdOf("e-hong"), CareStatus.Contacting, true));

        var logs = await db.CareLogs.AsNoTracking().ToListAsync(Ct);
        logs.Count.ShouldBe(2);
        logs.ShouldAllBe(l => l.Channel == CareChannel.Call && l.ByName == "Giáo vụ");
    }
}
