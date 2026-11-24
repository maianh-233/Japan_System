using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Enrollments.Events;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Tests.Domain;

public sealed class EnrollmentTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    private static Enrollment New() => Enrollment.Create(StudentId.New(), Guid.NewGuid(), new BranchId(Guid.NewGuid()), Today);

    [Fact]
    public void Ghi_danh_moi_dang_hoc_va_phat_su_kien()
    {
        var e = New();
        (e.Status, e.IsActive).ShouldBe((EnrollmentStatus.Studying, true));
        e.DomainEvents.OfType<StudentEnrolled>().ShouldHaveSingleItem();
    }

    [Fact]
    public void HV07_bao_luu_va_HV08_hoc_lai()
    {
        var e = New();
        e.Reserve(Today.AddMonths(1), " Đi công tác ", Today).IsSuccess.ShouldBeTrue();
        (e.Status, e.ReservedUntil, e.IsActive).ShouldBe((EnrollmentStatus.Reserved, Today.AddMonths(1), true));
        e.Note.ShouldBe("Bảo lưu đến 20/12/2026 — Đi công tác");

        e.Reserve(Today.AddMonths(2), "x", Today).Error.Code.ShouldBe("students.enrollment.not_studying");
        e.Resume(Today).IsSuccess.ShouldBeTrue();
        (e.Status, e.ReservedUntil, e.Note).ShouldBe((EnrollmentStatus.Studying, null, "Học lại từ 20/11/2026"));
        e.Resume(Today).Error.Code.ShouldBe("students.enrollment.not_reserved");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void HV07_E2_ngay_bao_luu_khong_sau_hom_nay(int days) =>
        New().Reserve(Today.AddDays(days), "Ốm", Today).Error.Code.ShouldBe("students.enrollment.invalid_reserve_until");

    [Fact]
    public void HV07_E2_bao_luu_qua_6_thang_va_thieu_ly_do()
    {
        New().Reserve(Today.AddMonths(6).AddDays(1), "Ốm", Today).Error.Code.ShouldBe("students.enrollment.invalid_reserve_until");
        New().Reserve(Today.AddMonths(6), "Ốm", Today).IsSuccess.ShouldBeTrue();
        New().Reserve(Today.AddMonths(1), " ", Today).Error.Code.ShouldBe("students.enrollment.reason_required");
    }

    [Fact]
    public void HV10_cho_nghi_can_ly_do_va_ket_thuc_mot_lan()
    {
        var e = New();
        e.Drop(Today, "").Error.Code.ShouldBe("students.enrollment.reason_required");
        e.Drop(Today, "Chuyển nhà").IsSuccess.ShouldBeTrue();
        (e.Status, e.EndedAt, e.Note, e.IsActive).ShouldBe((EnrollmentStatus.Dropped, Today, "Chuyển nhà", false));
        e.DomainEvents.OfType<EnrollmentEnded>().ShouldHaveSingleItem().Status.ShouldBe(EnrollmentStatus.Dropped);
        e.Drop(Today, "lại").Error.Code.ShouldBe("students.enrollment.already_ended");
    }

    [Fact]
    public void HV10_cho_nghi_ca_khi_dang_bao_luu()
    {
        var e = New();
        e.Reserve(Today.AddDays(10), "Ốm", Today);
        e.Drop(Today, "Lớp bị hủy: không đủ HV").IsSuccess.ShouldBeTrue();
        e.ReservedUntil.ShouldBeNull();
    }

    [Fact]
    public void HV11_hoan_thanh_chi_ap_cho_dang_hoc()
    {
        var studying = New();
        studying.Complete(Today).IsSuccess.ShouldBeTrue();
        studying.Status.ShouldBe(EnrollmentStatus.Completed);

        var reserved = New();
        reserved.Reserve(Today.AddDays(5), "Ốm", Today);
        reserved.Complete(Today).Error.Code.ShouldBe("students.enrollment.not_studying");
        reserved.Status.ShouldBe(EnrollmentStatus.Reserved);
    }

    [Fact]
    public void HV09_chuyen_lop_noi_hai_ghi_danh()
    {
        var old = New();
        var next = Enrollment.Create(old.StudentId, Guid.NewGuid(), old.BranchId, Today, old.Id, note: "Chuyển từ lớp GVV_N4-K10 — Đổi lịch");
        old.TransferTo(next.Id, "GVV_N4-K11", "", Today).Error.Code.ShouldBe("students.enrollment.reason_required");
        old.TransferTo(next.Id, "GVV_N4-K11", "Đổi lịch", Today).IsSuccess.ShouldBeTrue();
        (old.Status, old.TransferredToId, old.Note).ShouldBe((EnrollmentStatus.Transferred, next.Id, "Chuyển sang lớp GVV_N4-K11 — Đổi lịch"));
        next.TransferredFromId.ShouldBe(old.Id);
        old.TransferTo(next.Id, "X", "y", Today).Error.Code.ShouldBe("students.enrollment.already_ended");
    }
}
